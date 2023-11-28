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

    private bool _needRestart;
    
    public PluginPackageViewModel(Package package, IHttpService httpService, IPaths paths, ILogger logger, IPluginService pluginService) : 
        base(package, "Plugin", Path.Combine(paths.PluginsDirectory, package.Id!), httpService, logger)
    {
        _pluginService = pluginService;
        
        Plugin = _pluginService.InstalledPlugins.FirstOrDefault(x => x.Id == package.Id);
    }
    
    protected override void Install()
    {
        //Load Plugin
        if (!_needRestart)
        {
            Plugin = _pluginService.AddPlugin(ExtractionFolder);
            Status = PackageStatus.Installed;
        }
        else
        {
            Status = PackageStatus.NeedRestart;
        }
    }

    protected override void Uninstall()
    {
        if (Plugin != null)
        {
            _pluginService.RemovePlugin(Plugin);

            if (Plugin.IsCompatible)
            {
                _needRestart = true;
                Status = PackageStatus.NeedRestart;
            }
            else
            {
                Status = PackageStatus.Available;
            }
            Plugin = null;
        }
        else if(Status is PackageStatus.Installed)
        {
            Status = PackageStatus.Available;
        }
    }
}