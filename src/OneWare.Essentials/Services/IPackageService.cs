using System.ComponentModel;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;

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

    public Task<bool> InstallAsync(Package package);
}