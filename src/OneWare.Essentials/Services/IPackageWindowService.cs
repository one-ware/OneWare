using Avalonia.Controls;

namespace OneWare.Essentials.Services;

public interface IPackageWindowService
{
    /// <summary>
    /// Refreshes the package list in the UI.
    /// </summary>
    Task RefreshPackagesAsync();
    /// <summary>
    /// Shows the extension manager view.
    /// </summary>
    Control ShowExtensionManager();
    /// <summary>
    /// Shows the extension manager for a specific category.
    /// </summary>
    Control? ShowExtensionManager(string category, string? subcategory);
    /// <summary>
    /// Shows the extension manager focused on a package ID.
    /// </summary>
    Task<bool> ShowExtensionManagerAsync(string packageId);
    /// <summary>
    /// Shows the extension manager and attempts installation.
    /// </summary>
    Task<bool> ShowExtensionManagerAndTryInstallAsync(string packageId);
    /// <summary>
    /// Quickly installs a package by ID.
    /// </summary>
    Task<bool> QuickInstallPackageAsync(string packageId);
}
