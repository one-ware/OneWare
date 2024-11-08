using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Models;

public class PluginPackageModel : PackageModel
{
    private readonly IPluginService _pluginService;

    private bool _needRestart;

    private IPlugin? _plugin;

    public PluginPackageModel(Package package, IHttpService httpService, ILogger logger, IPaths paths,
        IApplicationStateService applicationStateService, IPluginService pluginService)
        : base(package, "Plugin", Path.Combine(paths.PluginsDirectory, package.Id!), httpService, logger,
            applicationStateService)
    {
        _pluginService = pluginService;

        Plugin = _pluginService.InstalledPlugins.FirstOrDefault(x => x.Id == package.Id);
    }

    public IPlugin? Plugin
    {
        get => _plugin;
        set
        {
            SetProperty(ref _plugin, value);
            if (_plugin is not null)
            {
                if (!_plugin.IsCompatible) InstalledVersionWarningText = _plugin.CompatibilityReport;
            }
            else
            {
                InstalledVersionWarningText = null;
            }
        }
    }

    protected override void Install(PackageTarget target)
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
        else if (Status is PackageStatus.Installed)
        {
            Status = PackageStatus.Available;
        }
    }

    public override async Task<CompatibilityReport> CheckCompatibilityAsync(PackageVersion version)
    {
        if (version.CompatibilityUrl != null || Package.SourceUrl != null)
        {
            var depsUrl = version.CompatibilityUrl ?? $"{Package.SourceUrl}/{version.Version}/compatibility.txt";

            var deps = await HttpService.DownloadTextAsync(depsUrl);

            return PluginCompatibilityChecker.CheckCompatibility(deps);
        }
        return PluginCompatibilityChecker.CheckCompatibility(null);
    }
}