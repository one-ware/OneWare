using System.Text.Json;
using OneWare.Core.Models;
using OneWare.Core.ModuleLogic;
using OneWare.SDK.Helpers;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Core.Services;

public class PluginService : IPluginService
{
    private static JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    
    private readonly IModuleCatalog _moduleCatalog;
    private readonly IModuleManager _moduleManager;

    private readonly string _pluginDirectory;
    
    public List<IPlugin> InstalledPlugins { get; } = new();

    public PluginService(IModuleCatalog moduleCatalog, IModuleManager moduleManager, IPaths paths)
    {
        _moduleCatalog = moduleCatalog;
        _moduleManager = moduleManager;

        _pluginDirectory = Path.Combine(paths.SessionDirectory, "Plugins");
        Directory.CreateDirectory(_pluginDirectory);
    }
    
    public IPlugin AddPlugin(string path)
    {
        var plugin = new Plugin(Path.GetFileName(path), path);
        InstalledPlugins.Add(plugin);
        if (CheckCompatibility(path) is {} report)
        {
            plugin.IsCompatible = false;
            plugin.CompatibilityReport = report;
            return plugin;
        }
        try
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

                plugin.IsCompatible = true;
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
        return plugin;
    }

    private string? CheckCompatibility(string path)
    {
        try
        {
            var pluginName = Path.GetFileName(path);
            
            var compatibilityIssues = string.Empty;
            
            //Dependency check
            var depFilePath = Path.Combine(path, "oneware.json");

            if (!File.Exists(depFilePath))
            {
                compatibilityIssues += $"Extension {pluginName} incompatible:\n\noneware.json not found in plugin folder\n";
                return compatibilityIssues;
            }

            var packageManifest = JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(depFilePath), _jsonSerializerOptions);

            if (packageManifest?.Dependencies is { } deps)
            {
                foreach (var dep in deps)
                {
                    var minVersion = Version.Parse(dep.MinVersion ?? "1000");
                    var maxVersion = Version.Parse(dep.MaxVersion ?? "0");

                    var coreDep = AppDomain.CurrentDomain.GetAssemblies()
                        .SingleOrDefault(x => x.GetName().Name == dep.Name)?.GetName();

                    if (coreDep == null)
                    {
                        compatibilityIssues += $"Dependency {dep.Name} not found\n";
                        continue;
                    }

                    if (coreDep.Version < minVersion)
                        compatibilityIssues += $"{dep.Name} v{coreDep.Version} is older than min required v{minVersion}\n";
                    if (coreDep.Version > maxVersion)
                        compatibilityIssues += $"{dep.Name} v{coreDep.Version} is newer than max required v{maxVersion}\n";
                }
            }

            if (compatibilityIssues.Length > 0)
            {
                compatibilityIssues = $"Extension {pluginName} incompatible:\n\n" + compatibilityIssues;
                return compatibilityIssues;
            }
        }
        catch (Exception e)
        {
            return e.Message;
        }
        return null;
    }

    public void RemovePlugin(IPlugin plugin)
    {
        try
        {
            if (Directory.Exists(plugin.Path))
            {
                Directory.Delete(plugin.Path, true);
            }
            InstalledPlugins.Remove(plugin);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }
}