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

    public PluginService(IModuleCatalog moduleCatalog, IModuleManager moduleManager, IPaths paths)
    {
        _moduleCatalog = moduleCatalog;
        _moduleManager = moduleManager;

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
}