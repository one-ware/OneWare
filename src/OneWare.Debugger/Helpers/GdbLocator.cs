using System.Runtime.InteropServices;
using OneWare.Essentials.Helpers;

namespace OneWare.Debugger.Helpers;

public static class GdbLocator
{
    private static readonly string[] BinaryNames =
    [
        "gdb-multiarch",
        "gdb"
    ];
    
    public static string? Find(string? nativeToolsDirectory = null)
    {
        foreach (var binaryName in BinaryNames)
        {
            var fileName = binaryName + PlatformHelper.ExecutableExtension;

            var bundled = FindInDirectory(nativeToolsDirectory, fileName);
            if (bundled != null) return bundled;
            
            var onPath = PlatformHelper.GetFullPath(fileName);
            if (onPath != null && File.Exists(onPath)) return onPath;
        }
        
        return DefaultLocations().FirstOrDefault(File.Exists);
    }

    private static string? FindInDirectory(string? directory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return null;

        try
        {
            return Directory.EnumerateFiles(directory, fileName, SearchOption.AllDirectories).FirstOrDefault();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static IEnumerable<string> DefaultLocations()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return ["/opt/homebrew/bin/gdb", "/usr/local/bin/gdb"];

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return ["/usr/bin/gdb", "/usr/local/bin/gdb"];

        return [];
    }
}