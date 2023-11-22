using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using OneWare.Core.ModuleLogic;
using OneWare.SDK.Extensions;
using OneWare.SDK.Helpers;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
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
    private readonly Dictionary<string, string> _failedPlugins = new();

    public IEnumerable<string> InstalledPlugins => _plugins.Keys;
    
    public IEnumerable<string> FailedPlugins => _failedPlugins.Keys;

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
        try
        {
            var pluginName = Path.GetFileName(path);

            //Dependency check
            var depFilePath = Path.Combine(path, "oneware.json");
            
            if (!File.Exists(depFilePath))
                throw new Exception($"Extension {pluginName} incompatible! oneware.json not found in Module");
            
            var packageManifest = JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(depFilePath));

            if (packageManifest?.Dependencies is {} deps)
            {
                foreach (var dep in deps)
                {
                    var minVersion = Version.Parse(dep.MinVersion ?? "1000");
                    var maxVersion = Version.Parse(dep.MaxVersion ?? "0");

                    var coreDep = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(x => x.GetName().Name == dep.Name)?.GetName();
                    
                    if(coreDep == null) throw new Exception($"Extension {pluginName} incompatible! Dependency {dep.Name} not found!");

                    if (minVersion < coreDep.Version) throw new Exception($"Extension {pluginName} incompatible! MinVersion of {dep.Name} is too low!");
                    if (maxVersion > coreDep.Version) throw new Exception($"Extension {pluginName} incompatible! MinVersion of {dep.Name} is too high!");
                }
            }
            
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
        catch (Exception e)
        {
            _failedPlugins.Add(Path.GetFileName(path), path);
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
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