using OneWare.PackageManager.Enums;
using OneWare.PackageManager.Serializer;
using OneWare.SDK.Models;
using OneWare.SDK.Services;

namespace OneWare.PackageManager.ViewModels;

public class PluginPackageViewModel : PackageViewModel
{
    private readonly IPluginService _pluginService;
    
    private IPlugin? _plugin;

    public IPlugin? Plugin
    {
        get => _plugin;
        set
        {
            SetProperty(ref _plugin, value);
            if (_plugin is not null)
            {
                if (!_plugin.IsCompatible)
                {
                    WarningText = _plugin.CompatibilityReport;
                }
            }
            else
            {
                WarningText = null;
            }
        }
    }
    
    public PluginPackageViewModel(Package package, IHttpService httpService, IPaths paths, ILogger logger, IPluginService pluginService) : 
        base(package, "Plugin", paths.PluginsDirectory, httpService, logger)
    {
        _pluginService = pluginService;
        
        Plugin = _pluginService.InstalledPlugins.FirstOrDefault(x => x.Id == package.Id);
    }

    protected override void Install(string path)
    {
        //Load Plugin
        Plugin = _pluginService.AddPlugin(path);
    }

    protected override void Uninstall()
    {
        if (Plugin != null)
        {
            _pluginService.RemovePlugin(Plugin);

            if (Plugin.IsCompatible)
            {
                Status = PackageStatus.NeedRestart;
            }
            Plugin = null;
        }
    }
}