using System.Reflection;
using System.Runtime.InteropServices;
using OneWare.Essentials.Helpers;

namespace OneWare.Core.Services;

public static class PluginNativeLibraryResolver
{
    private static readonly HashSet<Assembly> ConfiguredAssemblies = [];
    private static readonly Lock ConfiguredAssembliesLock = new();

    public static void Configure(
        string pluginPath,
        IEnumerable<Assembly> assemblies,
        Action<Assembly>? resolverAlreadyConfigured = null)
    {
        foreach (var assembly in assemblies)
        {
            lock (ConfiguredAssembliesLock)
            {
                if (!ConfiguredAssemblies.Add(assembly))
                    continue;
            }

            try
            {
                NativeLibrary.SetDllImportResolver(
                    assembly,
                    (libraryName, _, _) => Resolve(pluginPath, libraryName));
            }
            catch (InvalidOperationException)
            {
                // An assembly can provide its own resolver; leave it unchanged.
                resolverAlreadyConfigured?.Invoke(assembly);
            }
        }
    }

    internal static IReadOnlyList<string> GetCandidatePaths(
        string pluginPath,
        string libraryName,
        string applicationBaseDirectory)
    {
        var libraryFileName = PlatformHelper.GetLibraryFileName(libraryName);
        var platformNativeDirectory = Path.Combine(
            pluginPath,
            "runtimes",
            PlatformHelper.PlatformIdentifier,
            "native");

        return
        [
            Path.Combine(platformNativeDirectory, libraryFileName),
            Path.Combine(platformNativeDirectory, $"lib{libraryFileName}"),
            Path.Combine(pluginPath, libraryFileName),
            Path.Combine(pluginPath, $"lib{libraryFileName}"),
            Path.Combine(applicationBaseDirectory, libraryFileName),
            Path.Combine(applicationBaseDirectory, $"lib{libraryFileName}")
        ];
    }

    private static IntPtr Resolve(string pluginPath, string libraryName)
    {
        foreach (var libraryPath in GetCandidatePaths(pluginPath, libraryName, AppContext.BaseDirectory))
        {
            if (File.Exists(libraryPath) && NativeLibrary.TryLoad(libraryPath, out var handle))
                return handle;
        }

        return NativeLibrary.TryLoad(libraryName, out var fallbackHandle) ? fallbackHandle : IntPtr.Zero;
    }
}
