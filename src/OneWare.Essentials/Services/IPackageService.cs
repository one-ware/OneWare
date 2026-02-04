using System.ComponentModel;
using Avalonia.Media;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;

namespace OneWare.Essentials.Services;

public interface IPackageService : INotifyPropertyChanged
{
    public bool IsUpdating { get; }

    public Dictionary<string, PackageModel> Packages { get; }

    public event EventHandler PackagesUpdated;

    public void RegisterPackage(Package package);

    public void RegisterPackageRepository(string url);

    public PackageModel? GetPackageModel(Package package);

    public Task<bool> LoadPackagesAsync();

    public Task<PackageInstallResult> InstallAsync(Package package);

    public Task<string?> DownloadLicenseAsync(Package package);

    public Task<IImage?> DownloadPackageIconAsync(Package package);
}