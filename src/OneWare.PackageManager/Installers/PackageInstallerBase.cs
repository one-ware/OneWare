using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Services;

namespace OneWare.PackageManager.Installers;

public abstract class PackageInstallerBase : IPackageInstaller
{
    public abstract string PackageType { get; }

    public virtual PackageTarget? SelectTarget(Package package, PackageVersion version)
    {
        var currentTarget = PlatformHelper.Platform.ToString().ToLower();
        return version.Targets?.FirstOrDefault(x => x.Target?.Replace("-", "") == currentTarget)
               ?? version.Targets?.FirstOrDefault(x => x.Target == "all");
    }

    public virtual Task<CompatibilityReport> CheckCompatibilityAsync(Package package, PackageVersion version,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CompatibilityReport(true));
    }

    public virtual Task PrepareRemoveAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public abstract Task<PackageInstallerResult> InstallAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default);

    public abstract Task<PackageInstallerResult> RemoveAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default);
}
