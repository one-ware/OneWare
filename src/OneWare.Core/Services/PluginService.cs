using System.Reflection;
using System.Runtime.InteropServices;
using OneWare.Core.Models;
using OneWare.Essentials.Helpers;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Core.ModuleLogic;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using Microsoft.Extensions.Logging;

namespace OneWare.Core.Services;

public class PluginService : IPluginService
{
    private readonly OneWareModuleCatalog _moduleCatalog;
    private readonly OneWareModuleManager _moduleManager;
    private readonly ModuleServiceRegistry _moduleServiceRegistry;

    private readonly string _pluginDirectory;
    private readonly HashSet<string> _resolverSetAssemblies = new();

    private List<Assembly> _initAssemblies;

    public PluginService(OneWareModuleCatalog moduleCatalog, OneWareModuleManager moduleManager,
        ModuleServiceRegistry moduleServiceRegistry, IPaths paths)
    {
        _moduleCatalog = moduleCatalog;
        _moduleManager = moduleManager;
        _moduleServiceRegistry = moduleServiceRegistry;

        _initAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

        _pluginDirectory = Path.Combine(paths.SessionDirectory, "Plugins");
        Directory.CreateDirectory(_pluginDirectory);
    }

    public List<IPlugin> InstalledPlugins { get; } = new();

    public IPlugin AddPlugin(string path)
    {
        // Update known assemblies to avoid redundant resolver registration
        _initAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

        var plugin = new Plugin(Path.GetFileName(path), path);
        InstalledPlugins.Add(plugin);

        if (PluginCompatibilityChecker.CheckCompatibilityPath(path) is { IsCompatible: false } test)
        {
            plugin.CompatibilityReport = test.Report;
            ContainerLocator.Container?.Resolve<ILogger>().Error($"Plugin {path} failed loading:\n{test.Report}");
            return plugin;
        }

        plugin.IsCompatible = true;

        try
        {
            var realPath = Path.Combine(_pluginDirectory, Path.GetFileName(path));
            PlatformHelper.CopyDirectory(path, realPath);

            var addedModules = LoadModulesFromPath(realPath);

            if (addedModules.Count > 0 && ContainerLocator.Container != null)
            {
                var pluginServices = new ServiceCollection();
                _moduleManager.RegisterModuleServices(pluginServices, addedModules);
                _moduleServiceRegistry.AddDescriptors(pluginServices);
                if (_moduleManager.InitializationCompleted)
                    _moduleManager.InitializeModules(ContainerLocator.Current, addedModules);
            }

            //We should not use that anymore, since it can break compatibility with code signed apps
            //We keep it for now except on MacOS
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                SetupNativeImports(realPath);
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }

        return plugin;
    }

    public void RemovePlugin(IPlugin plugin)
    {
        try
        {
            if (Directory.Exists(plugin.Path)) Directory.Delete(plugin.Path, true);
            InstalledPlugins.Remove(plugin);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }

    private IReadOnlyList<IOneWareModule> LoadModulesFromPath(string path)
    {
        var assemblies = new List<Assembly>();
        foreach (var file in Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories))
        {
            try
            {
                assemblies.Add(Assembly.LoadFrom(file));
            }
            catch (Exception ex)
            {
                ContainerLocator.Container?.Resolve<ILogger>()
                    .Warning($"Skipping plugin assembly '{Path.GetFileName(file)}': {ex.Message}", ex);
            }
        }

        var added = new List<IOneWareModule>();
        foreach (var assembly in assemblies)
        {
            added.AddRange(_moduleCatalog.AddModulesFromAssembly(assembly));
        }

        foreach (var module in added)
        {
            ContainerLocator.Container?.Resolve<ILogger>()
                .Log($"Module '{module.Id}' loaded");
        }

        return added;
    }

    private void SetupNativeImports(string pluginPath)
    {
        var newAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(x => !_initAssemblies.Contains(x));

        foreach (var assembly in newAssemblies)
        {
            _initAssemblies.Add(assembly);

            if (assembly.FullName == null) continue;

            if (_resolverSetAssemblies.Contains(assembly.FullName))
                continue;

            try
            {
                NativeLibrary.SetDllImportResolver(assembly, (libraryName, _, _) =>
                {
                    // Try 1
                    var libFileName = PlatformHelper.GetLibraryFileName(libraryName);
                    var libPath = Path.Combine(pluginPath, libFileName);

                    // Try 2 : look in runtimes folder
                    if (!File.Exists(libPath))
                    {
                        libPath = Path.Combine(pluginPath, "runtimes", PlatformHelper.PlatformIdentifier, "native",
                            libFileName);
                    }

                    // Try 3: add lib infront of it
                    if (!File.Exists(libPath))
                    {
                        libPath = Path.Combine(pluginPath, $"lib{libFileName}");
                    }

                    // Try 4 : look in (plugin) runtimes folder with lib infront
                    if (!File.Exists(libPath))
                    {
                        libPath = Path.Combine(pluginPath, "runtimes", PlatformHelper.PlatformIdentifier, "native",
                            $"lib{libFileName}");
                    }

                    // Try 5: MacOS weirdness, look in (own) base folder
                    // TODO find out why this is not automatic in MacOS, and why even without this we don't have issues
                    if (!File.Exists(libPath))
                    {
                        libPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, libFileName);
                    }

                    // Try 6: Same as 5 but added lib Prefix
                    if (!File.Exists(libPath))
                    {
                        libPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"lib{libFileName}");
                    }

                    if (NativeLibrary.TryLoad(libPath, out var customHandle))
                    {
                        return customHandle;
                    }

                    if (NativeLibrary.TryLoad(libraryName, out var handle))
                    {
                        return handle;
                    }

                    Console.WriteLine($"Loading native library {libraryName} failed");
                    return IntPtr.Zero;
                });

                _resolverSetAssemblies.Add(assembly.FullName);
            }
            catch (InvalidOperationException)
            {
                // This assembly already has a resolver — log and continue
                ContainerLocator.Container.Resolve<ILogger>().Warning(
                    $"Skipping resolver setup for {assembly.FullName}, resolver already set.");
            }
        }
    }
}
