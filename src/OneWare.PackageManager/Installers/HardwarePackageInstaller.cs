using OneWare.Essentials.Enums;
using OneWare.Essentials.PackageManager;

namespace OneWare.PackageManager.Installers;

public class HardwarePackageInstaller : PackageInstallerBase
{
    public override string PackageType => "Hardware";

    public override Task<PackageInstallerResult> InstallAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PackageInstallerResult(PackageStatus.Installed));
    }

    public override Task<PackageInstallerResult> RemoveAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PackageInstallerResult(PackageStatus.Available));
    }
}
