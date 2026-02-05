using OneWare.Essentials.Enums;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Installers;

public class PluginPackageInstaller : PackageInstallerBase
{
    private readonly IHttpService _httpService;
    private readonly IPluginService _pluginService;
    private readonly HashSet<string> _restartRequired = new();

    public PluginPackageInstaller(IHttpService httpService, IPluginService pluginService)
    {
        _httpService = httpService;
        _pluginService = pluginService;
    }

    public override string PackageType => "Plugin";

    public override async Task<CompatibilityReport> CheckCompatibilityAsync(Package package, PackageVersion version,
        CancellationToken cancellationToken = default)
    {
        if (version.CompatibilityUrl != null || package.SourceUrl != null)
        {
            var depsUrl = version.CompatibilityUrl ?? $"{package.SourceUrl}/{version.Version}/compatibility.txt";
            var deps = await _httpService.DownloadTextAsync(depsUrl);
            return PluginCompatibilityChecker.CheckCompatibility(deps);
        }

        return PluginCompatibilityChecker.CheckCompatibility(null);
    }

    public override Task<PackageInstallerResult> InstallAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Package.Id != null && _restartRequired.Contains(context.Package.Id))
        {
            return Task.FromResult(new PackageInstallerResult(PackageStatus.NeedRestart));
        }

        var plugin = _pluginService.AddPlugin(context.ExtractionPath);
        var warning = plugin != null && !plugin.IsCompatible ? plugin.CompatibilityReport : null;

        return Task.FromResult(new PackageInstallerResult(PackageStatus.Installed, warning));
    }

    public override Task<PackageInstallerResult> RemoveAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default)
    {
        var id = context.Package.Id;
        if (id == null) return Task.FromResult(new PackageInstallerResult(PackageStatus.Available));

        var plugin = _pluginService.InstalledPlugins.FirstOrDefault(x => x.Id == id);

        if (plugin != null)
        {
            _pluginService.RemovePlugin(plugin);
            if (plugin.IsCompatible)
            {
                _restartRequired.Add(id);
                return Task.FromResult(new PackageInstallerResult(PackageStatus.NeedRestart));
            }
        }

        return Task.FromResult(new PackageInstallerResult(PackageStatus.Available));
    }
}
