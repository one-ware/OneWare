using System.ComponentModel;
using Avalonia.Media;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;

namespace OneWare.Essentials.Services;

public interface IPackageManager : INotifyPropertyChanged
{
    bool IsUpdating { get; }

    IReadOnlyDictionary<string, IPackageState> Packages { get; }

    event EventHandler PackagesUpdated;

    event EventHandler<PackageProgressEventArgs> PackageProgress;

    void RegisterPackage(Package package);

    void RegisterPackageRepository(string url);

    Task<bool> RefreshAsync();

    Task<PackageInstallResult> InstallAsync(Package package, PackageVersion? version = null,
        bool includePrerelease = false, bool ignoreCompatibility = false);

    Task<PackageInstallResult> InstallAsync(string packageId, PackageVersion? version = null,
        bool includePrerelease = false, bool ignoreCompatibility = false);

    Task<PackageInstallResult> UpdateAsync(string packageId, PackageVersion? version = null,
        bool includePrerelease = false, bool ignoreCompatibility = false);

    Task<bool> RemoveAsync(string packageId);

    Task<CompatibilityReport> CheckCompatibilityAsync(string packageId, PackageVersion version);

    Task<string?> DownloadLicenseAsync(Package package);

    Task<IImage?> DownloadPackageIconAsync(Package package);
}
