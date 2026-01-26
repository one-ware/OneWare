using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using Microsoft.Extensions.Logging;

namespace OneWare.PackageManager.Models;

public class NativeToolPackageModel(
    Package package,
    IHttpService httpService,
    ILogger logger,
    IPaths paths,
    ISettingsService settingsService,
    IApplicationStateService applicationStateService,
    IChildProcessService childProcessService)
    : PackageModel(package, "NativeTool", Path.Combine(paths.NativeToolsDirectory, package.Id!), httpService, logger,
        applicationStateService)
{
    protected override PackageTarget? SelectTarget(PackageVersion version)
    {
        var target = base.SelectTarget(version);

        //If OSX-ARM64 is not available, try to use OSX-X64 to use with Rosetta
        if (target == null && PlatformHelper.Platform is PlatformId.OsxArm64)
            target = version.Targets?.FirstOrDefault(x => x.Target == "osx-x64");

        return target;
    }

    protected override void Install(PackageTarget target)
    {
        if (target.AutoSetting == null) return;
        foreach (var shortCut in target.AutoSetting)
            if (shortCut is { RelativePath: not null, SettingKey: not null })
            {
                var fullPath = Path.Combine(ExtractionFolder, shortCut.RelativePath);
                settingsService.SetSettingValue(shortCut.SettingKey, "");
                settingsService.SetSettingValue(shortCut.SettingKey, fullPath);
                settingsService.Save(paths.SettingsPath);
            }

        Status = PackageStatus.Installed;
    }

    protected override async Task PrepareRemoveAsync(PackageTarget target)
    {
        await base.PrepareRemoveAsync(target);

        if (target.AutoSetting == null) return;
        foreach (var shortCut in target.AutoSetting)
            if (shortCut is { RelativePath: not null, SettingKey: not null })
            {
                var fullPath = Path.Combine(ExtractionFolder, shortCut.RelativePath);

                foreach (var process in childProcessService.GetChildProcesses(fullPath).ToArray())
                    childProcessService.Kill(process);

                await Task.Delay(100);

                childProcessService.GetChildProcesses(fullPath);
            }
    }

    protected override void Uninstall()
    {
        Status = PackageStatus.Available;
    }
}