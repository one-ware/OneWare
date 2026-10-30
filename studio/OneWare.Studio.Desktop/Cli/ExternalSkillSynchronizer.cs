using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using OneWare.Essentials.PackageManager;

namespace OneWare.Studio.Desktop.Cli;

internal sealed class ExternalSkillSynchronizer(
    string documentsDirectory,
    string pluginsDirectory,
    string packagesDirectory,
    string appName,
    string homeDirectory,
    TextWriter output,
    TextWriter errors)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private string ManifestPath => Path.Combine(documentsDirectory, "oneware-skills.json");
    private string SkillsRoot => Path.Combine(homeDirectory, ".agents", "skills");

    public int Synchronize(IReadOnlyList<ExternalSkillProviderSource> providers)
    {
        if (string.IsNullOrWhiteSpace(homeDirectory) || !Path.IsPathFullyQualified(homeDirectory))
            throw new InvalidOperationException("The user home directory could not be determined.");
        var agentsRoot = Path.Combine(homeDirectory, ".agents");
        Directory.CreateDirectory(agentsRoot);
        EnsureNotLink(agentsRoot);
        var lockPath = Path.Combine(agentsRoot, ".oneware-skills.lock");
        if (File.Exists(lockPath)) EnsureNotLink(lockPath);
        using var syncLock = new FileStream(lockPath,
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var manifest = LoadManifest();
        var packages = LoadPackages();
        var desired = DiscoverSkills(providers, packages);
        if (desired.Count == 0) output.WriteLine("No external agent skills found.");
        Directory.CreateDirectory(SkillsRoot);
        EnsureNotLink(SkillsRoot);

        var conflicts = false;
        foreach (var skill in desired.Values.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            var destination = Path.Combine(SkillsRoot, skill.Name);
            manifest.Skills.TryGetValue(skill.Name, out var previous);
            if (previous is not null && !string.Equals(previous.Source, skill.Source, StringComparison.Ordinal))
            {
                errors.WriteLine($"Skill '{skill.Name}' belongs to another extension; skipping.");
                conflicts = true;
                continue;
            }

            if (Directory.Exists(destination) || File.Exists(destination))
            {
                if (previous is null || !Directory.Exists(destination) || !Matches(destination, previous.Files))
                {
                    errors.WriteLine($"Skill '{skill.Name}' is not managed by OneWare or has local changes; skipping.");
                    conflicts = true;
                    continue;
                }
            }
            else if (previous is not null)
            {
                errors.WriteLine($"Managed skill '{skill.Name}' is missing; skipping.");
                conflicts = true;
                continue;
            }

            var newFiles = Snapshot(skill.Directory);
            if (previous is not null && SameFiles(previous.Files, newFiles))
            {
                if (previous.Version != skill.Version)
                {
                    previous.Version = skill.Version;
                    SaveManifest(manifest);
                }
                output.WriteLine($"Unchanged: {skill.Name}");
                continue;
            }

            InstallSkill(skill, destination, newFiles, manifest, previous);
            output.WriteLine($"{(previous is null ? "Installed" : "Updated")}: {skill.Name}");
        }

        var cleanupBlocked = false;
        foreach (var (name, entry) in manifest.Skills.ToArray())
        {
            if (desired.ContainsKey(name) || entry.Source.StartsWith("builtin:", StringComparison.Ordinal))
                continue;

            if (entry.Source.StartsWith("package:", StringComparison.Ordinal))
            {
                var packageId = entry.Source["package:".Length..];
                // A missing database cannot prove that a package was uninstalled.
                if (packages is null)
                {
                    cleanupBlocked = true;
                    continue;
                }
                if (packages.ContainsKey(packageId) || Directory.Exists(Path.Combine(pluginsDirectory, packageId)))
                    continue;
            }

            var destination = Path.Combine(SkillsRoot, name);
            if (Directory.Exists(destination) && Matches(destination, entry.Files))
            {
                RemoveSkill(destination, name, manifest, entry);
                output.WriteLine($"Removed: {name}");
            }
            else
            {
                errors.WriteLine($"Obsolete skill '{name}' is missing or has local changes; skipping.");
                conflicts = true;
            }
        }

        if (cleanupBlocked)
        {
            errors.WriteLine("Package database is missing; obsolete package extension skills were not removed.");
            conflicts = true;
        }

        return conflicts ? 1 : 0;
    }

    private Dictionary<string, SkillSource> DiscoverSkills(
        IReadOnlyList<ExternalSkillProviderSource> providers,
        IReadOnlyDictionary<string, InstalledPackage>? packages)
    {
        var skills = new Dictionary<string, SkillSource>(PathComparer);
        var pluginRoot = Path.GetFullPath(pluginsDirectory);
        foreach (var providerSource in providers)
        {
            var pluginPath = Path.GetFullPath(providerSource.PluginPath);
            var parent = Path.GetDirectoryName(pluginPath);
            var isPackage = parent is not null && PathComparer.Equals(parent, pluginRoot);
            var packageId = Path.GetFileName(pluginPath);
            var source = providerSource.BuiltInId is not null
                ? "builtin:" + providerSource.BuiltInId
                : isPackage ? "package:" + packageId : "path:" + pluginPath;
            var version = providerSource.Version;
            if (providerSource.BuiltInId is null && isPackage && packages is not null &&
                packages.TryGetValue(packageId, out var package))
                version = package.InstalledVersion;

            var roots = providerSource.Provider.GetSkillDirectories()
                ?? throw new InvalidDataException($"Skill provider in '{pluginPath}' returned no directory list.");
            foreach (var root in roots)
            {
                if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root) || !Directory.Exists(root))
                    throw new InvalidDataException($"Skill directory '{root}' from '{pluginPath}' is missing or not an absolute path.");
                EnsureNotLink(root);
                foreach (var directory in Directory.EnumerateDirectories(root))
                {
                    EnsureNotLink(directory);
                    var name = Path.GetFileName(directory);
                    ValidateName(name);
                    var skillFile = Path.Combine(directory, "SKILL.md");
                    if (!File.Exists(skillFile))
                        throw new InvalidDataException($"Skill '{name}' has no SKILL.md.");
                    EnsureNotLink(skillFile);
                    ValidateSkillFile(skillFile, name);
                    if (!skills.TryAdd(name, new SkillSource(name, directory, source, version)))
                        throw new InvalidDataException($"Multiple extensions provide the skill '{name}'.");
                }
            }
        }
        return skills;
    }

    private SkillManifest LoadManifest()
    {
        if (!File.Exists(ManifestPath))
            return new SkillManifest { Version = 1 };
        EnsureNotLink(ManifestPath);
        var manifest = JsonSerializer.Deserialize<SkillManifest>(File.ReadAllText(ManifestPath), JsonOptions)
                       ?? throw new InvalidDataException("The skill manifest is empty.");
        if (manifest.Version != 1 || manifest.Skills is null)
            throw new InvalidDataException("The skill manifest has an unsupported format.");
        var validated = new Dictionary<string, SkillEntry>(PathComparer);
        foreach (var (name, entry) in manifest.Skills)
        {
            ValidateName(name);
            if (entry is null || string.IsNullOrWhiteSpace(entry.Source) || entry.Files is null ||
                !entry.Files.ContainsKey("SKILL.md") ||
                !validated.TryAdd(name, entry))
                throw new InvalidDataException($"Invalid skill manifest entry '{name}'.");
            if (entry.Source.StartsWith("package:", StringComparison.Ordinal))
            {
                var packageId = entry.Source["package:".Length..];
                if (string.IsNullOrWhiteSpace(packageId) || packageId is "." or ".." ||
                    packageId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    packageId.Contains('/') || packageId.Contains('\\'))
                    throw new InvalidDataException($"Invalid package ID in skill manifest entry '{name}'.");
            }
            else if (!entry.Source.StartsWith("path:", StringComparison.Ordinal) &&
                     !(entry.Source.StartsWith("builtin:", StringComparison.Ordinal) &&
                       entry.Source.Length > "builtin:".Length))
                throw new InvalidDataException($"Invalid source in skill manifest entry '{name}'.");
        }
        manifest.Skills = validated;
        return manifest;
    }

    private IReadOnlyDictionary<string, InstalledPackage>? LoadPackages()
    {
        var path = Path.Combine(packagesDirectory, $"{appName.ToLowerInvariant().Replace(" ", "")}-packages.json");
        if (!File.Exists(path))
            return null;
        EnsureNotLink(path);
        var packages = JsonSerializer.Deserialize<InstalledPackage[]>(File.ReadAllText(path), JsonOptions)
                       ?? throw new InvalidDataException("The package database is empty.");
        var result = new Dictionary<string, InstalledPackage>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in packages)
        {
            if (package is null || string.IsNullOrWhiteSpace(package.Id) || !result.TryAdd(package.Id, package))
                throw new InvalidDataException("The package database contains an invalid or duplicate package ID.");
        }
        return result;
    }

    private void InstallSkill(SkillSource skill, string destination, Dictionary<string, string> files,
        SkillManifest manifest, SkillEntry? previous)
    {
        var temporary = Path.Combine(SkillsRoot, ".oneware-" + Guid.NewGuid().ToString("N"));
        var backup = Path.Combine(SkillsRoot, ".oneware-" + Guid.NewGuid().ToString("N"));
        var hadPrevious = previous is not null;
        var movedOld = false;
        var movedNew = false;
        try
        {
            CopyTree(skill.Directory, temporary);
            if (!SameFiles(files, Snapshot(temporary)))
                throw new IOException($"Skill '{skill.Name}' changed during installation.");
            if (hadPrevious)
            {
                if (!Matches(destination, previous!.Files))
                    throw new IOException($"Skill '{skill.Name}' changed during installation.");
                Directory.Move(destination, backup);
                movedOld = true;
            }
            Directory.Move(temporary, destination);
            movedNew = true;
            manifest.Skills[skill.Name] = new SkillEntry { Source = skill.Source, Version = skill.Version, Files = files };
            SaveManifest(manifest);
        }
        catch
        {
            if (movedNew && Directory.Exists(destination))
                Directory.Move(destination, temporary);
            if (movedOld && Directory.Exists(backup))
                Directory.Move(backup, destination);
            if (previous is null) manifest.Skills.Remove(skill.Name);
            else manifest.Skills[skill.Name] = previous;
            throw;
        }
        finally
        {
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
        if (movedOld) Directory.Delete(backup, true);
    }

    private void RemoveSkill(string destination, string name, SkillManifest manifest, SkillEntry entry)
    {
        var backup = Path.Combine(SkillsRoot, ".oneware-" + Guid.NewGuid().ToString("N"));
        Directory.Move(destination, backup);
        try
        {
            if (!Matches(backup, entry.Files))
                throw new IOException($"Skill '{name}' changed during removal.");
            manifest.Skills.Remove(name);
            SaveManifest(manifest);
        }
        catch
        {
            manifest.Skills[name] = entry;
            Directory.Move(backup, destination);
            throw;
        }
        Directory.Delete(backup, true);
    }

    private void SaveManifest(SkillManifest manifest)
    {
        Directory.CreateDirectory(documentsDirectory);
        var temporary = Path.Combine(documentsDirectory, ".oneware-skills-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(manifest, JsonOptions));
            File.Move(temporary, ManifestPath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static Dictionary<string, string> Snapshot(string root)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        Collect(root, root, result);
        return result;
    }

    private static void Collect(string root, string directory, Dictionary<string, string> result)
    {
        EnsureNotLink(directory);
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            EnsureNotLink(path);
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (Directory.Exists(path))
            {
                result.Add(relative + "/", "directory");
                Collect(root, path, result);
            }
            else
            {
                using var stream = File.OpenRead(path);
                var hash = Convert.ToHexString(SHA256.HashData(stream));
                if (!OperatingSystem.IsWindows())
                    hash += ":" + (int)File.GetUnixFileMode(path);
                result.Add(relative, hash);
            }
        }
    }

    private static void CopyTree(string source, string destination)
    {
        EnsureNotLink(source);
        Directory.CreateDirectory(destination);
        foreach (var path in Directory.EnumerateFileSystemEntries(source))
        {
            EnsureNotLink(path);
            var target = Path.Combine(destination, Path.GetFileName(path));
            if (Directory.Exists(path)) CopyTree(path, target);
            else
            {
                File.Copy(path, target);
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(target, File.GetUnixFileMode(path));
            }
        }
    }

    private static bool Matches(string directory, Dictionary<string, string> expected) =>
        SameFiles(expected, Snapshot(directory));

    private static bool SameFiles(Dictionary<string, string> first, Dictionary<string, string> second) =>
        first.Count == second.Count && first.All(pair => second.TryGetValue(pair.Key, out var value) && pair.Value == value);

    private static void EnsureNotLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"Symbolic links are not supported: '{path}'.");
    }

    private static void ValidateName(string name)
    {
        if (name.Length is < 1 or > 64 || !Regex.IsMatch(name, @"^[a-z0-9]+(?:-[a-z0-9]+)*$"))
            throw new InvalidDataException($"Invalid skill name '{name}'. Use lowercase letters, digits and single hyphens.");
    }

    private static void ValidateSkillFile(string path, string name)
    {
        var content = File.ReadAllText(path);
        var match = Regex.Match(content, @"\A---\s*\r?\n(?<front>.*?)\r?\n---(?:\s*\r?\n|\s*$)",
            RegexOptions.Singleline);
        if (!match.Success)
            throw new InvalidDataException($"Skill '{name}' has invalid YAML front matter.");
        var nameMatch = Regex.Match(match.Groups["front"].Value,
            @"(?m)^name[ \t]*:[ \t]*(?:'(?<name>[a-z0-9-]+)'|""(?<name>[a-z0-9-]+)""|(?<name>[a-z0-9-]+))[ \t]*(?:#.*)?\r?$");
        var descriptionMatch = Regex.Match(match.Groups["front"].Value, @"(?m)^description[ \t]*:[ \t]*\S");
        if (!nameMatch.Success || nameMatch.Groups["name"].Value != name || !descriptionMatch.Success)
            throw new InvalidDataException($"Skill '{name}' needs a matching name and a description in SKILL.md.");
    }

    private sealed record SkillSource(string Name, string Directory, string Source, string? Version);

    private sealed class SkillManifest
    {
        [JsonRequired]
        public int Version { get; set; }
        [JsonRequired]
        public Dictionary<string, SkillEntry> Skills { get; set; } = new(PathComparer);
    }

    private sealed class SkillEntry
    {
        [JsonRequired]
        public string Source { get; set; } = "";
        public string? Version { get; set; }
        [JsonRequired]
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.Ordinal);
    }
}
