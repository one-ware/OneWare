using OneWare.Essentials.Enums;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Installers;

public class OnnxRuntimePackageInstaller : PackageInstallerBase
{
    public override string PackageType => "OnnxRuntime";

    public override Task<PackageInstallerResult> InstallAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default)
    {
        ContainerLocator.Container.Resolve<ISettingsService>().SetSettingValue("OnnxRuntime_SelectedRuntime",
            Path.GetFileName(context.ExtractionPath));
        ContainerLocator.Container.Resolve<ISettingsService>()
            .Save(ContainerLocator.Container.Resolve<IPaths>().SettingsPath);
        return Task.FromResult(new PackageInstallerResult(PackageStatus.NeedRestart));
    }

    public override Task<PackageInstallerResult> RemoveAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default)
    {
        var settingsService = ContainerLocator.Container.Resolve<ISettingsService>();
        var selectedOnnxRuntime = settingsService.GetSettingValue<string>("OnnxRuntime_SelectedRuntime");
        if (selectedOnnxRuntime == Path.GetFileName(context.ExtractionPath))
        {
            settingsService.SetSettingValue("OnnxRuntime_SelectedRuntime", "onnxruntime-cpu");
            settingsService.Save(ContainerLocator.Container.Resolve<IPaths>().SettingsPath);
        }
        return Task.FromResult(new PackageInstallerResult(PackageStatus.NeedRestart));
    }
}