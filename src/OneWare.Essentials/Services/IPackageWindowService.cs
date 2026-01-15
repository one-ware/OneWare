using Avalonia.Controls;

namespace OneWare.Essentials.Services;

public interface IPackageWindowService
{
    Task RefreshPackagesAsync();
    Control ShowExtensionManager();
    Control? ShowExtensionManager(string category, string? subcategory);
    Task<bool> ShowExtensionManagerAndTryInstallAsync(string packageId);
    Task<bool> QuickInstallPackageAsync(string packageId);
}