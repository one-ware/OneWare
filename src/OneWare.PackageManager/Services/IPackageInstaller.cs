using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;

namespace OneWare.PackageManager.Services;

public interface IPackageInstaller
{
    string PackageType { get; }

    PackageTarget? SelectTarget(Package package, PackageVersion version);

    Task<CompatibilityReport> CheckCompatibilityAsync(Package package, PackageVersion version,
        CancellationToken cancellationToken = default);

    Task PrepareRemoveAsync(PackageInstallContext context, CancellationToken cancellationToken = default);

    Task<PackageInstallerResult> InstallAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default);

    Task<PackageInstallerResult> RemoveAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default);
}
