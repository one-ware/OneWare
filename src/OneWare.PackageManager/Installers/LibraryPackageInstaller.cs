using System;
using System.IO;
using OneWare.Essentials.Enums;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Installers;

public class LibraryPackageInstaller : PackageInstallerBase
{
    public override string GetExtractionPath(Package package, IPaths paths)
    {
        if (package.Id == null) throw new InvalidOperationException("Package Id is required.");
        return Path.Combine(paths.PackagesDirectory, "Libraries", package.Id);
    }

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
