using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.PackageManager.Models;

namespace OneWare.PackageManager.ViewModels;

public class PackageQuickInstallViewModel : FlexibleWindowViewModelBase
{
    private readonly IPackageService _packageService;

    public PackageQuickInstallViewModel(IPackageState package, IPackageService packageService)
    {
        Package = package;
        _packageService = packageService;
        
        Title = $"{(package.Status is PackageStatus.UpdateAvailable ? "Update" : "Install")} {Package.Package.Name}";

        _ = ResolveAsync();
    }

    public IPackageState Package { get; }

    public bool Success { get; private set; }

    public bool HasLicense => !string.IsNullOrEmpty(LicenseText);

    public string? LicenseText
    {
        get;
        set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(HasLicense));
        }
    }

    public IImage? Icon
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsLoading
    {
        get;
        set => SetProperty(ref field, value);
    }

    public AsyncRelayCommand<FlexibleWindow> InstallCommand => new(async window =>
    {
        var result = await _packageService.InstallAsync(Package.Package);
        
        Success = result.Status is PackageInstallResultReason.AlreadyInstalled or PackageInstallResultReason.Installed;

        if (!Success)
        {
            await ContainerLocator.Container.Resolve<IWindowService>().ShowMessageAsync("Installation failed",
                result.CompatibilityRecord?.Report ?? "Please try again later or check for OneWare Studio updates",
                MessageBoxIcon.Error);
        }
        if(Success)
            window?.Close();
    });

    private async Task ResolveAsync()
    {
        IsLoading = true;

        LicenseText = await _packageService.DownloadLicenseAsync(Package.Package);
        Icon = await _packageService.DownloadPackageIconAsync(Package.Package);

        IsLoading = false;
    }
}
