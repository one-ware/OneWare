using System.Threading;
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
    private readonly IWindowService? _windowService;
    private CancellationTokenSource? _installCts;

    public PackageQuickInstallViewModel(IPackageState package, IPackageService packageService, IWindowService? windowService = null)
    {
        Package = package;
        _packageService = packageService;
        _windowService = windowService;
        InstallCommand = new AsyncRelayCommand<FlexibleWindow>(InstallAsync, _ => !IsInstalling);
        CancelCommand = new RelayCommand<FlexibleWindow>(window =>
        {
            if (IsInstalling)
            {
                _installCts?.Cancel();
            }
            else window?.Close();
        });
        
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

    public bool IsInstalling
    {
        get;
        private set
        {
            if (SetProperty(ref field, value)) InstallCommand.NotifyCanExecuteChanged();
        }
    }

    public string? ResultMessage { get; private set => SetProperty(ref field, value); }
    public RelayCommand<FlexibleWindow> CancelCommand { get; }
    public AsyncRelayCommand<FlexibleWindow> InstallCommand { get; }

    public override bool OnWindowClosing(FlexibleWindow window)
    {
        if (Success || !IsInstalling) return base.OnWindowClosing(window);
        _installCts?.Cancel();
        return false; // Keep partial completion/cancellation visible until the operation returns.
    }

    private async Task InstallAsync(FlexibleWindow? window)
    {
        if (IsInstalling) return;

        _installCts = new CancellationTokenSource();
        ResultMessage = null;
        IsInstalling = true;

        try
        {
            var result = await PackageOperationReview.RunAsync(_packageService,
                _windowService ?? ContainerLocator.Container.Resolve<IWindowService>(), [new(Package.Package.Id!)], window?.Host, _installCts.Token);

            Success = result.Status is PackageInstallResultReason.AlreadyInstalled or PackageInstallResultReason.Installed;
            ResultMessage = PackageOperationReview.DescribeResult(result);
            if (Success)
                window?.Close();
        }
        finally
        {
            IsInstalling = false;
            _installCts?.Dispose();
            _installCts = null;
        }
    }

    private async Task ResolveAsync()
    {
        IsLoading = true;

        try { Icon = await _packageService.DownloadPackageIconAsync(Package.Package); }
        catch { /* Optional imagery must not block the operation review. */ }
        finally { IsLoading = false; }
    }
}
