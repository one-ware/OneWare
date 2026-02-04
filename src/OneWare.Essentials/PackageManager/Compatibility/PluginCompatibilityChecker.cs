using System.Reflection;

namespace OneWare.Essentials.PackageManager.Compatibility;

public class PluginCompatibilityChecker
{
    public static CompatibilityReport CheckCompatibilityPath(string path)
    {
        try
        {
            var pluginName = Path.GetFileName(path);

            var depFilePath = Path.Combine(path, "compatibility.txt");

            var compatibilityIssues = "";

            if (!File.Exists(depFilePath))
            {
                compatibilityIssues += "compatibility.txt not found in plugin folder\n";
                return new CompatibilityReport(false, compatibilityIssues);
            }

            return CheckCompatibility(File.ReadAllText(depFilePath));
        }
        catch (Exception e)
        {
            return new CompatibilityReport(false, e.Message);
        }
    }

    public static CompatibilityReport CheckCompatibility(string? deps)
    {
        try
        {
            var compatibilityIssues = "";
            var records = new List<CompatibilityRecord>();
            var suggestions = new List<string>();
            var studioUpdateRequired = false;
            var isCompatible = true;

            if (deps == null) return new CompatibilityReport(false, "Error checking compatibility");

            var depsList = deps.Trim().Split('\n');

            var coreDeps = GetReferencedAssembliesRecursive(Assembly.GetEntryAssembly()!);

            foreach (var dep in depsList)
            {
                var parts = dep.Split(':');
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
                    var message = $"Dependency {dependencyName} not found";
                    compatibilityIssues += $"{message}\n";
                    records.Add(new CompatibilityRecord(
                        dependencyName,
                        dependencyVersionFull,
                        null,
                        CompatibilityRecordKind.MissingDependency,
                        message));
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
                    var message =
                        $"Dependency {dependencyName} requires {required}, but provided is {provided}";
                    compatibilityIssues += $"{message}\n";
                    if (requiresCoreUpdate)
                    {
                        records.Add(new CompatibilityRecord(
                            dependencyName,
                            required,
                            provided,
                            CompatibilityRecordKind.RequiresCoreUpdate,
                            message));
                        suggestions.Add(
                            $"Update OneWare core to at least {required} for {dependencyName}.");
                        studioUpdateRequired = true;
                    }
                    else if (pluginOutdated)
                    {
                        records.Add(new CompatibilityRecord(
                            dependencyName,
                            required,
                            provided,
                            CompatibilityRecordKind.PluginOutdated,
                            message));
                        suggestions.Add(
                            $"Update the plugin to a version compatible with core {provided} for {dependencyName}.");
                    }
                    isCompatible = false;
                }
                else if (pluginOutdated)
                {
                    var message =
                        $"Dependency {dependencyName} targets {required}, but core provides {provided}";
                    records.Add(new CompatibilityRecord(
                        dependencyName,
                        required,
                        provided,
                        CompatibilityRecordKind.PluginOutdated,
                        message));
                    suggestions.Add(
                        $"Update the plugin to a version compatible with core {provided} for {dependencyName}.");
                }
            }

            return new CompatibilityReport(
                isCompatible,
                compatibilityIssues.Length == 0 ? null : compatibilityIssues,
                records,
                suggestions)
            {
                StudioUpdateRequired = studioUpdateRequired
            };
        }
        catch (Exception e)
        {
            return new CompatibilityReport(false, e.Message);
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
