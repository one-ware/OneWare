using System.ComponentModel;
using Avalonia.Media;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;

namespace OneWare.Essentials.Services;

public interface IPackageService : INotifyPropertyChanged
{
    /// <summary>
    /// True while package operations are running.
    /// </summary>
    bool IsUpdating { get; }
    
    bool IsLoaded { get; }

    /// <summary>
    /// Registered packages keyed by ID.
    /// </summary>
    IReadOnlyDictionary<string, IPackageState> Packages { get; }

    /// <summary>
    /// Fired when packages are refreshed or changed.
    /// </summary>
    event EventHandler PackagesUpdated;

    /// <summary>
    /// Fired during package operations to report progress.
    /// </summary>
    event EventHandler<PackageProgressEventArgs> PackageProgress;

    /// <summary>
    /// Registers a package definition.
    /// </summary>
    void RegisterPackage(Package package);

    /// <summary>
    /// Registers a package repository URL.
    /// </summary>
    void RegisterPackageRepository(string url);

    /// <summary>
    /// Registers a package installer for a package type.
    /// </summary>
    void RegisterInstaller<T>(string packageType) where T : IPackageInstaller;

    /// <summary>
    /// Refreshes package metadata from repositories.
    /// </summary>
    Task<bool> RefreshAsync();

    /// <summary>
    /// Installs a package by definition.
    /// </summary>
    Task<PackageInstallResult> InstallAsync(Package package, PackageVersion? version = null,
        bool includePrerelease = false, bool ignoreCompatibility = false);

    /// <summary>
    /// Installs a package by ID.
    /// </summary>
    Task<PackageInstallResult> InstallAsync(string packageId, PackageVersion? version = null,
        bool includePrerelease = false, bool ignoreCompatibility = false);

    /// <summary>
    /// Updates an installed package.
    /// </summary>
    Task<PackageInstallResult> UpdateAsync(string packageId, PackageVersion? version = null,
        bool includePrerelease = false, bool ignoreCompatibility = false);

    /// <summary>
    /// Removes an installed package.
    /// </summary>
    Task<bool> RemoveAsync(string packageId);

    /// <summary>
    /// Checks compatibility for a specific package version.
    /// </summary>
    Task<CompatibilityReport> CheckCompatibilityAsync(string packageId, PackageVersion version);

    /// <summary>
    /// Downloads a package license.
    /// </summary>
    Task<string?> DownloadLicenseAsync(Package package);

    /// <summary>
    /// Downloads a package icon.
    /// </summary>
    Task<IImage?> DownloadPackageIconAsync(Package package);
}
