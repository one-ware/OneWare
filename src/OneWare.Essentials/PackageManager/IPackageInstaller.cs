using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.PackageManager;

public interface IPackageInstaller
{
    string GetExtractionPath(Package package, IPaths paths);

    PackageTarget? SelectTarget(Package package, PackageVersion version);

    Task<CompatibilityReport> CheckCompatibilityAsync(Package package, PackageVersion version,
        CancellationToken cancellationToken = default);

    Task PrepareRemoveAsync(PackageInstallContext context, CancellationToken cancellationToken = default);

    Task<PackageInstallerResult> InstallAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default);

    Task<PackageInstallerResult> RemoveAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default);
}
