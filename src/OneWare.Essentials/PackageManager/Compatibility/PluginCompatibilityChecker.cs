using System.Reflection;
using DryIoc;

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
                compatibilityIssues += $"compatibility.txt not found in plugin folder\n";
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

            if (deps == null) return new CompatibilityReport(false, "Error checking compatibility");

            var depsList = deps.Trim().Split('\n');

            var coreDeps = GetReferencedAssembliesRecursive(Assembly.GetEntryAssembly()!);
            
            foreach (var dep in depsList)
            {
                var parts = dep.Split(':');
                var dependencyName = parts[0].Trim();
                var versionString = parts[1].Trim();
                var dependencyVersion = Version.Parse(NormalizeVersion(versionString));

                switch (dependencyName)
                {
                    //TODO
                    case "OneWare.Markdown.Avalonia.Tight":
                    case "OneWare.Markdown.Avalonia.SyntaxHigh":
                    case "OneWare.AvaloniaEdit":
                    case "OneWare.AvaloniaEdit.TextMate":
                        continue;
                }
                
                if (!coreDeps.TryGetValue(dependencyName, out var coreDep))
                {
                    compatibilityIssues += $"Dependency {dependencyName} not found\n";
                    
                    continue;
                }

                if (coreDep.Version < dependencyVersion)
                    compatibilityIssues +=
                        $"Required {dependencyName} : {dependencyVersion} > {coreDep.Version}\n";
                if (coreDep.Version > dependencyVersion)
                    compatibilityIssues +=
                        $"Required {dependencyName} : {dependencyVersion} < {coreDep.Version}\n";
            }

            return new CompatibilityReport(compatibilityIssues.Length == 0, compatibilityIssues);
        }
        catch (Exception e)
        {
            return new CompatibilityReport(false, e.Message);
        }
    }

    static string NormalizeVersion(string version)
    {
        string[] parts = version.Split('.');
        while (parts.Length < 4)
        {
            Array.Resize(ref parts, parts.Length + 1);
            parts[^1] = "0";
        }

        return string.Join('.', parts);
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
                    {
                        if (!result.ContainsKey(refAsm.Name!))
                            toVisit.Enqueue(refAsm);
                    }
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