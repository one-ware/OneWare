using System.Reflection;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.PackageManager.Compatibility;

public class PluginCompatibilityChecker
{
    public static CompatibilityReport CheckCompatibilityPath(string path) => CheckCompatibilityPath(path, null);

    public static CompatibilityReport CheckCompatibilityPath(string path,
        IReadOnlyDictionary<string, AssemblyName>? providedAssemblies)
    {
        try
        {
            var depFilePath = Path.Combine(path, "compatibility.txt");
            
            if (!File.Exists(depFilePath))
            {
                ContainerLocator.Container.Resolve<ILogger>().Error("Compatibility Check failed: compatibility.txt not found in plugin folder");
                return new CompatibilityReport(false, []);
            }

            return CheckCompatibility(File.ReadAllText(depFilePath), providedAssemblies);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return new CompatibilityReport(false, []);
        }
    }

    public static CompatibilityReport CheckCompatibility(string? deps) => CheckCompatibility(deps, null);

    public static CompatibilityReport CheckCompatibility(string? deps,
        IReadOnlyDictionary<string, AssemblyName>? providedAssemblies)
    {
        try
        {
            var records = new List<CompatibilityIssue>();
            var isCompatible = true;

            if (deps == null)
            {
                ContainerLocator.Container.Resolve<ILogger>().Error("Compatibility Check failed");
                return new CompatibilityReport(false, records);
            }

            var depsList = deps.Trim().Split('\n');

            var coreDeps = GetReferencedAssembliesRecursive(Assembly.GetEntryAssembly()!);
            // Declared providers may fill missing plugin assemblies, never override core requirements.
            foreach (var (name, assembly) in providedAssemblies ?? new Dictionary<string, AssemblyName>())
                coreDeps.TryAdd(name, assembly);

            foreach (var dep in depsList)
            {
                var parts = dep.Split(':');

                if (parts.Length < 2)
                {
                    isCompatible = false;
                    break;
                }
                var dependencyName = parts[0].Trim();
                var versionString = parts[1].Trim();

                var dependencyVersionFull = Version.Parse(NormalizeVersion(versionString));

                switch (dependencyName)
                {
                    //TODO
                    case "OneWare.Markdown.Avalonia.Tight":
                    case "OneWare.Markdown.Avalonia.SyntaxHigh":
                    case "OneWare.AvaloniaEdit":
                    case "OneWare.AvaloniaEdit.TextMate":
                        continue;
                }

                if (!coreDeps.TryGetValue(dependencyName, out var coreDep) || coreDep.Version == null)
                {
                    records.Add(new CompatibilityIssue(
                        dependencyName,
                        dependencyVersionFull,
                        null,
                        CompatibilityRecordKind.MissingDependency));
                    isCompatible = false;
                    continue;
                }

                var required = dependencyVersionFull;
                var provided = NormalizeVersion(coreDep.Version);
                var comparison = provided.CompareTo(required);
                var requiresCoreUpdate = comparison < 0;
                var pluginOutdated = comparison > 0;

                if (provided.Major != required.Major ||
                    provided.Minor != required.Minor ||
                    provided.Build < required.Build)
                {
                    if (requiresCoreUpdate)
                    {
                        records.Add(new CompatibilityIssue(
                            dependencyName,
                            required,
                            provided,
                            CompatibilityRecordKind.RequiresCoreUpdate));
                    }
                    else if (pluginOutdated)
                    {
                        records.Add(new CompatibilityIssue(
                            dependencyName,
                            required,
                            provided,
                            CompatibilityRecordKind.PluginOutdated));
                    }
                    isCompatible = false;
                }
                else if (pluginOutdated)
                {
                    records.Add(new CompatibilityIssue(
                        dependencyName,
                        required,
                        provided,
                        CompatibilityRecordKind.PluginOutdated));
                }
            }

            return new CompatibilityReport(isCompatible, records);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return new CompatibilityReport(false, []);
        }
    }

    private static string NormalizeVersion(string version)
    {
        var parts = version.Split('.');
        while (parts.Length < 4)
        {
            Array.Resize(ref parts, parts.Length + 1);
            parts[^1] = "0";
        }

        return string.Join('.', parts);
    }

    public static IReadOnlyDictionary<string, AssemblyName> ReadProvidedAssemblies(IEnumerable<string> paths)
    {
        var assemblies = new Dictionary<string, AssemblyName>(StringComparer.Ordinal);
        foreach (var path in paths.Distinct())
        {
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"Declared dependency directory missing: {path}");
            foreach (var file in Directory.EnumerateFiles(path, "*.dll", SearchOption.AllDirectories)
                         .Where(file => ShouldProbePluginAssembly(path, file)).OrderBy(file => file, StringComparer.Ordinal))
            {
                AssemblyName name;
                try { name = AssemblyName.GetAssemblyName(file); }
                catch (BadImageFormatException) { continue; }
                if (name.Name == null) continue;
                if (assemblies.TryGetValue(name.Name, out var existing) && existing.FullName != name.FullName)
                    throw new InvalidOperationException($"Conflicting dependency assembly providers for {name.Name}.");
                assemblies[name.Name] = name;
            }
        }
        return assemblies;
    }

    // Keep provider discovery identical to the loader, especially for RID-specific managed assets.
    public static bool ShouldProbePluginAssembly(string pluginPath, string filePath)
    {
        var segments = Path.GetRelativePath(pluginPath, filePath)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (segments.Length < 2 || !segments[0].Equals("runtimes", StringComparison.OrdinalIgnoreCase))
            return true;
        return segments.Length >= 4 &&
               segments[1].Equals(PlatformHelper.PlatformIdentifier, StringComparison.OrdinalIgnoreCase) &&
               segments[2].Equals("lib", StringComparison.OrdinalIgnoreCase);
    }

    private static Version NormalizeVersion(Version version)
    {
        var major = Math.Max(version.Major, 0);
        var minor = Math.Max(version.Minor, 0);
        var build = Math.Max(version.Build, 0);
        var revision = Math.Max(version.Revision, 0);

        return new Version(major, minor, build, revision);
    }

    public static Dictionary<string, AssemblyName> GetReferencedAssembliesRecursive(Assembly rootAssembly)
    {
        var result = new Dictionary<string, AssemblyName>();
        var toVisit = new Queue<AssemblyName>(rootAssembly.GetReferencedAssemblies());

        while (toVisit.Count > 0)
        {
            var current = toVisit.Dequeue();

            if (result.ContainsKey(current.Name!))
                continue;

            result[current.Name!] = current;

            // Try to find the physical assembly to inspect further
            try
            {
                var loadedAsm = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == current.Name);

                if (loadedAsm != null)
                {
                    var referenced = loadedAsm.GetReferencedAssemblies();
                    foreach (var refAsm in referenced)
                        if (!result.ContainsKey(refAsm.Name!))
                            toVisit.Enqueue(refAsm);
                }
                // Else: it's referenced but not loaded and we stop here
            }
            catch
            {
                // Ignore load errors
            }
        }

        return result;
    }
}
