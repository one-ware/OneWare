using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.PackageManager.ViewModels;

public class PackageQuickInstallViewModel : FlexibleWindowViewModelBase
{
    private readonly IPackageService _packageService;

    public PackageQuickInstallViewModel(PackageModel package, IPackageService packageService)
    {
        Package = package;
        _packageService = packageService;

        Title = $"Install {Package.Package.Name}";

        _ = ResolveAsync();
    }

    public PackageModel Package { get; }

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