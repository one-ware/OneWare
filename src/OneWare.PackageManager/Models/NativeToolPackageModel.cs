using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Models
{
    public class NativeToolPackageModel : PackageModel
    {
        private readonly ISettingsService _settingsService;
        private readonly IChildProcessService _childProcessService;
        private readonly PlatformHelper _platformHelper;

        public NativeToolPackageModel(
            Package package,
            IHttpService httpService,
            ILogger<PackageModel> logger,
            IPaths paths,
            ISettingsService settingsService,
            IApplicationStateService applicationStateService,
            IChildProcessService childProcessService,
            PlatformHelper platformHelper)
            : base(package, "NativeTool", Path.Combine(paths.NativeToolsDirectory, package.Id!), httpService, logger,
                applicationStateService, platformHelper)
        {
            _settingsService = settingsService;
            _childProcessService = childProcessService;
            _platformHelper = platformHelper;
        }

        protected override PackageTarget? SelectTarget(PackageVersion version)
        {
            var target = base.SelectTarget(version);
            // If OSX-ARM64 is not available, try to use OSX-X64 to use with Rosetta
            if (target == null && _platformHelper.Platform is PlatformId.OsxArm64)
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
                    _settingsService.SetSettingValue(shortCut.SettingKey, "");
                    _settingsService.SetSettingValue(shortCut.SettingKey, fullPath);
                    _settingsService.Save(fullPath);
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
                    foreach (var process in _childProcessService.GetChildProcesses(fullPath).ToArray())
                        _childProcessService.Kill(process);
                    await Task.Delay(100);
                    _childProcessService.GetChildProcesses(fullPath);
                }
        }

        protected override void Uninstall()
        {
            Status = PackageStatus.Available;
        }
    }
}
