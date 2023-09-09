using OneWare.Shared.Extensions;
using OneWare.Shared.Helpers;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Core.Services;

public class PluginService : IPluginService
{
    private readonly IModuleCatalog _moduleCatalog;
    private readonly IModuleManager _moduleManager;
    private readonly IPaths _paths;
    
    private readonly string _pluginDirectory;
    private readonly Dictionary<string, string> _plugins = new();

    public IEnumerable<string> InstalledPlugins => _plugins.Keys;

    public PluginService(IModuleCatalog moduleCatalog, IModuleManager moduleManager, IPaths paths)
    {
        _moduleCatalog = moduleCatalog;
        _moduleManager = moduleManager;
        _paths = paths;
        
        _pluginDirectory = Path.Combine(_paths.SessionDirectory, "Plugins");
        Directory.CreateDirectory(_pluginDirectory);
    }
    
    public void AddPlugin(string path)
    {
        var realPath = Path.Combine(_pluginDirectory, Path.GetFileName(path));
        PlatformHelper.CopyDirectory(path, realPath);
        
        var catalog = new DirectoryModuleCatalog()
        {
            ModulePath = realPath
        };
        catalog.Initialize();

        foreach (var module in catalog.Modules)
        {
            _moduleCatalog.AddModule(module);
            if(_moduleCatalog.Modules.FirstOrDefault()?.State == ModuleState.Initialized) 
                _moduleManager.LoadModule(module.ModuleName);

            _plugins.Add(module.ModuleName, path);
        }
    }

    public void RemovePlugin(string id)
    {
        _plugins.TryGetValue(id, out var path);

        if (path == null) return;
        
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
        _plugins.Remove(id);
    }
}