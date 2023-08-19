using OneWare.Shared.Extensions;
using OneWare.Shared.Helpers;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using Prism.Modularity;

namespace OneWare.Core.Services;

public class PluginService : IPluginService
{
    private readonly IModuleCatalog _moduleCatalog;
    private readonly IModuleManager _moduleManager;
    private readonly IPaths _paths;

    private readonly string _pluginDirectory;
    private List<string> _plugins = new();
    
    public IEnumerable<string> Plugins => _plugins;

    public PluginService(IModuleCatalog moduleCatalog, IModuleManager moduleManager, IPaths paths)
    {
        _moduleCatalog = moduleCatalog;
        _moduleManager = moduleManager;
        _paths = paths;

        _pluginDirectory = Path.Combine(paths.TempDirectory, "OneWare Modules").CheckNameDirectory();
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
        }
    }

    public void RemovePlugin(string id)
    {
        
    }
}