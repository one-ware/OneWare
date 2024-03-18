using OneWare.PackageManager.Models;

namespace OneWare.PackageManager.Services;

public interface IPackageService
{
    public Dictionary<string, PackageModel> Packages { get; }
    
    public event EventHandler? UpdateStarted;
    
    public event EventHandler? UpdateEnded;
    
    public void RegisterPackageRepository(string url);

    public Task<bool> LoadPackagesAsync();
}