using System.Reflection;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager.Compatibility;

namespace OneWare.Core.Services;

public static class PluginAssemblyLoader
{
    // Usually we can assume that all managed DLLs will be in the base dir of a plugin.
    // Some libraries ship in runtimes/arch/lib/...
    public static bool ShouldProbePluginAssembly(string pluginPath, string filePath)
    {
        var relativePath = Path.GetRelativePath(pluginPath, filePath);
        var pathSegments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (pathSegments.Length < 2 || !pathSegments[0].Equals("runtimes", StringComparison.OrdinalIgnoreCase))
            return true;

        if (pathSegments.Length < 4)
            return false;

        return pathSegments[1].Equals(PlatformHelper.PlatformIdentifier, StringComparison.OrdinalIgnoreCase)
               && pathSegments[2].Equals("lib", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryGetManagedAssemblyName(string filePath, out AssemblyName assemblyName)
    {
        try
        {
            assemblyName = AssemblyName.GetAssemblyName(filePath);
            return true;
        }
        catch (BadImageFormatException)
        {
        }
        catch (FileLoadException)
        {
        }

        assemblyName = null!;
        return false;
    }

    /// <summary>
    ///     Reads the assemblies a plugin requires (<c>compatibility.txt</c>) and the managed assemblies it ships.
    /// </summary>
    public static PluginLoadInfo Describe(string pluginPath)
    {
        var required = new List<string>();
        var provided = new List<string>();

        try
        {
            var compatibilityFile = Path.Combine(pluginPath, "compatibility.txt");
            if (File.Exists(compatibilityFile))
                required.AddRange(PluginCompatibilityChecker.ReadRequiredAssemblies(File.ReadAllText(compatibilityFile)));

            foreach (var file in Directory.GetFiles(pluginPath, "*.dll", SearchOption.AllDirectories))
                if (ShouldProbePluginAssembly(pluginPath, file) && TryGetManagedAssemblyName(file, out var name) &&
                    name.Name != null)
                    provided.Add(name.Name);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // An unreadable plugin keeps its position, loading it reports the actual problem
        }

        return new PluginLoadInfo(pluginPath, required, provided);
    }

    /// <summary>
    ///     Orders plugin folders so plugins that extend another plugin are loaded after it.
    /// </summary>
    public static IReadOnlyList<string> SortByDependencies(IEnumerable<string> pluginPaths)
    {
        var infos = pluginPaths.Select(Describe).ToList();
        return PluginLoadOrder.Sort(infos).Select(x => x.Path).ToList();
    }
}
