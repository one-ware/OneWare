using System.Reflection;
using OneWare.Essentials.Helpers;

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
}
