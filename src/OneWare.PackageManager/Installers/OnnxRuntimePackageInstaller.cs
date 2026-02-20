using OneWare.Essentials.Enums;
using OneWare.Essentials.PackageManager;

namespace OneWare.PackageManager.Installers;

public class OnnxRuntimePackageInstaller : PackageInstallerBase
{
    public override string PackageType => "OnnxRuntime";

    public override Task<PackageInstallerResult> InstallAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PackageInstallerResult(PackageStatus.NeedRestart));
    }

    public override Task<PackageInstallerResult> RemoveAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PackageInstallerResult(PackageStatus.NeedRestart));
    }
}
