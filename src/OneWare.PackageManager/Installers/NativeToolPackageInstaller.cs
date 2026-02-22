using System;
using System.IO;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Installers;

public class NativeToolPackageInstaller : PackageInstallerBase
{
    private readonly IChildProcessService _childProcessService;
    private readonly IPaths _paths;
    private readonly ISettingsService _settingsService;

    public NativeToolPackageInstaller(ISettingsService settingsService, IPaths paths,
        IChildProcessService childProcessService)
    {
        _settingsService = settingsService;
        _paths = paths;
        _childProcessService = childProcessService;
    }
    
    public override string GetExtractionPath(Package package, IPaths paths)
    {
        if (package.Id == null) throw new InvalidOperationException("Package Id is required.");
        return Path.Combine(paths.NativeToolsDirectory, package.Id);
    }

    public override PackageTarget? SelectTarget(Package package, PackageVersion version)
    {
        var target = base.SelectTarget(package, version);

        if (target == null && PlatformHelper.Platform is PlatformId.OsxArm64)
            target = version.Targets?.FirstOrDefault(x => x.Target == "osx-x64");

        return target;
    }

    public override Task<PackageInstallerResult> InstallAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Target.AutoSetting == null)
            return Task.FromResult(new PackageInstallerResult(PackageStatus.Installed));

        foreach (var shortCut in context.Target.AutoSetting)
            if (shortCut is { RelativePath: not null, SettingKey: not null })
            {
                var fullPath = Path.Combine(context.ExtractionPath, shortCut.RelativePath);
                _settingsService.SetSettingValue(shortCut.SettingKey, "");
                _settingsService.SetSettingValue(shortCut.SettingKey, fullPath);
                _settingsService.Save(_paths.SettingsPath);
            }

        return Task.FromResult(new PackageInstallerResult(PackageStatus.Installed));
    }

    public override async Task PrepareRemoveAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Target.AutoSetting == null) return;
        foreach (var shortCut in context.Target.AutoSetting)
            if (shortCut is { RelativePath: not null, SettingKey: not null })
            {
                var fullPath = Path.Combine(context.ExtractionPath, shortCut.RelativePath);

                foreach (var process in _childProcessService.GetChildProcesses(fullPath).ToArray())
                    _childProcessService.Kill(process);

                await Task.Delay(100, cancellationToken);
                _childProcessService.GetChildProcesses(fullPath);
            }
    }

    public override Task<PackageInstallerResult> RemoveAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PackageInstallerResult(PackageStatus.Available));
    }
}
