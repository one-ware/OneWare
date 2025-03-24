using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using OneWare.Core.Models;
using OneWare.Core.ModuleLogic;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Core.Services;

public class PluginService : IPluginService
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IModuleCatalog _moduleCatalog;
    private readonly IModuleManager _moduleManager;

    private readonly string _pluginDirectory;

    private List<Assembly> _initAssemblies;

    public PluginService(IModuleCatalog moduleCatalog, IModuleManager moduleManager, IPaths paths)
    {
        _moduleCatalog = moduleCatalog;
        _moduleManager = moduleManager;

        _initAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

        _pluginDirectory = Path.Combine(paths.SessionDirectory, "Plugins");
        Directory.CreateDirectory(_pluginDirectory);
    }

    public List<IPlugin> InstalledPlugins { get; } = new();

    public IPlugin AddPlugin(string path)
    {
        var plugin = new Plugin(Path.GetFileName(path), path);
        InstalledPlugins.Add(plugin);

        if (PluginCompatibilityChecker.CheckCompatibilityPath(path) is { IsCompatible: false } test)
        {
            plugin.CompatibilityReport = test.Report;
            ContainerLocator.Container.Resolve<ILogger>().Error($"Plugin {path} failed loading: \n {test.Report}");
            return plugin;
        }

        plugin.IsCompatible = true;

        try
        {
            var realPath = Path.Combine(_pluginDirectory, Path.GetFileName(path));
            PlatformHelper.CopyDirectory(path, realPath);

            var catalog = new DirectoryModuleCatalog
            {
                ModulePath = realPath
            };
            catalog.Initialize();

            foreach (var module in catalog.Modules)
            {
                _moduleCatalog.AddModule(module);
                if (_moduleCatalog.Modules.FirstOrDefault()?.State == ModuleState.Initialized)
                    _moduleManager.LoadModule(module.ModuleName);

                ContainerLocator.Container.Resolve<ILogger>()
                    .Log($"Module {module.ModuleName} loaded", ConsoleColor.Cyan, true);
            }
            
            SetupNativeImports(realPath);
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

    private void SetupNativeImports(string pluginPath)
    {
        var newAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(x => !_initAssemblies.Contains(x));
        
        foreach (var assembly in newAssemblies)
        {
            _initAssemblies.Add(assembly);

            try
            {
                NativeLibrary.SetDllImportResolver(assembly, (libraryName, _, _) =>
                {
                    var libFileName = PlatformHelper.GetLibraryFileName(libraryName);
                    var libPath = Path.Combine(pluginPath, libFileName);

                    if (!File.Exists(libPath))
                    {
                        // Alternative path for debug builds
                        libPath = Path.Combine(pluginPath, "runtimes", PlatformHelper.PlatformIdentifier, "native", libFileName);
                    }
                    
                    // Try to load the library from the plugin directory
                    if (NativeLibrary.TryLoad(libPath, out var customHandle))
                    {
                        return customHandle;
                    }

                    // Try the default system resolution as a fallback
                    if (NativeLibrary.TryLoad(libraryName, out var handle))
                    {
                        return handle;
                    }

                    Console.WriteLine($"Loading native library {libPath} failed");
                    
                    return IntPtr.Zero;
                });
            }
            catch (InvalidOperationException)
            {
                // Some assemblies do not support SetDllImportResolver, ignore them
            }
        }
    }
}