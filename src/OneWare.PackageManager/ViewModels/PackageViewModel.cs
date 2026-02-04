using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Models;

namespace OneWare.PackageManager.ViewModels;

public class PackageViewModel : ObservableObject
{
    private readonly IHttpService _httpService;
    private readonly IPackageService _packageService;
    private readonly IWindowService _windowService;
    private readonly IApplicationStateService _applicationStateService;

    private IPackageState _packageState;

    private IDisposable? _primaryButtonBrushSubscription;

    private bool _resolveImageStarted;

    private bool _resolveTabsStarted;

    public PackageViewModel(IPackageState packageState, IPackageService packageService, IHttpService httpService,
        IWindowService windowService, IApplicationStateService applicationStateService)
    {
        _packageState = packageState;
        _packageService = packageService;
        _httpService = httpService;
        _windowService = windowService;
        _applicationStateService = applicationStateService;

        RemoveCommand = new AsyncRelayCommand<Control?>(_ => _packageService.RemoveAsync(PackageState.Package.Id!),
            _ => PackageState.Status is PackageStatus.Installed or PackageStatus.UpdateAvailable
                or PackageStatus.UpdateAvailablePrerelease);

        InstallCommand = new AsyncRelayCommand<Control?>(
            x => ConfirmLicenseAndDownloadAsync(x, PackageState, SelectedVersionModel!.Version),
            _ => PackageState.Status is PackageStatus.Available);

        UpdateCommand = new AsyncRelayCommand<Control?>(_ =>
                _packageService.UpdateAsync(PackageState.Package.Id!, SelectedVersionModel!.Version),
            _ => PackageState.Status is PackageStatus.UpdateAvailable or PackageStatus.UpdateAvailablePrerelease);

        PackageState.WhenValueChanged(x => x.Status).Subscribe(_ => UpdateStatus());
        InitPackage();
    }

    public bool IsTabsResolved
    {
        get;
        set => SetProperty(ref field, value);
    }

    public IPackageState PackageState
    {
        get => _packageState;
        set
        {
            SetProperty(ref _packageState, value);
            InitPackage();
        }
    }

    public IImage? Image
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public ObservableCollection<PackageVersionModel> PackageVersionModels { get; } = new();
    public ObservableCollection<TabModel> Tabs { get; } = [];
    public ObservableCollection<LinkModel> Links { get; } = [];

    public PackageVersionModel? SelectedVersionModel
    {
        get;
        set
        {
            SetProperty(ref field, value);
            UpdateStatus();
            _ = CheckSelectedVersionCompatibilityAsync();
        }
    }

    public string PrimaryButtonText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public IBrush? PrimaryButtonBrush
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public ICommand? MainButtonCommand
    {
        get;
        set => SetProperty(ref field, value);
    }

    public AsyncRelayCommand<Control?> RemoveCommand { get; }

    public AsyncRelayCommand<Control?> InstallCommand { get; }

    public AsyncRelayCommand<Control?> UpdateCommand { get; }

    private void InitPackage()
    {
        Links.Clear();
        if (PackageState.Package.Links != null)
            Links.AddRange(PackageState.Package.Links.Select(x => new LinkModel(x.Name ?? "Link", x.Url ?? "")));

        PackageVersionModels.Clear();
        if (PackageState.Package.Versions != null)
            PackageVersionModels.AddRange(PackageState.Package.Versions
                .OrderByDescending(x =>
                {
                    if (Version.TryParse(x.Version, out var v)) return v;
                    return new Version(int.MaxValue, 0);
                })
                .Select(x => new PackageVersionModel(x)));

        var includePrerelease = PackageState.InstalledVersion?.IsPrerelease ?? false;

        SelectedVersionModel = PackageVersionModels.OrderBy(x => includePrerelease || x.Version.IsPrerelease)
            .FirstOrDefault(x => x.Version.MinStudioVersion == null
                                 || (Version.TryParse(x.Version.MinStudioVersion, out var minVersion)
                                     && Assembly.GetEntryAssembly()!.GetName().Version >= minVersion));

        _resolveTabsStarted = false;
        _resolveImageStarted = false;
        UpdateStatus();
        _ = ResolveIconAsync();
    }

    private void UpdateStatus()
    {
        Version.TryParse(SelectedVersionModel?.Version.Version ?? "", out var sV);
        Version.TryParse(PackageState.InstalledVersion?.Version ?? "", out var iV);

        MainButtonCommand = null;
        var primaryButtonBrushObservable = Application.Current!.GetResourceObservable("ThemeBorderMidBrush");
        switch (PackageState.Status)
        {
            case PackageStatus.Available:
                PrimaryButtonText = "Install";
                primaryButtonBrushObservable = Application.Current!.GetResourceObservable("ThemeAccentBrush");
                MainButtonCommand = InstallCommand;
                break;
            case PackageStatus.UpdateAvailable when sV > iV:
            case PackageStatus.UpdateAvailablePrerelease when sV > iV:
                PrimaryButtonText = "Update";
                primaryButtonBrushObservable = Application.Current!.GetResourceObservable("ThemeAccentBrush");
                MainButtonCommand = UpdateCommand;
                break;
            case PackageStatus.Installed:
            case PackageStatus.UpdateAvailable:
            case PackageStatus.UpdateAvailablePrerelease:
                PrimaryButtonText = "Remove";
                MainButtonCommand = RemoveCommand;
                break;
            case PackageStatus.Installing:
                PrimaryButtonText = "Installing...";
                break;
            case PackageStatus.Unavailable:
                PrimaryButtonText = "Unavailable";
                break;
            case PackageStatus.NeedRestart:
                PrimaryButtonText = "Restart Required";
                MainButtonCommand = new AsyncRelayCommand<Control?>(AskForRestartAsync);
                primaryButtonBrushObservable = Application.Current!.GetResourceObservable("ThemeControlMidBrush");
                break;
        }

        _primaryButtonBrushSubscription?.Dispose();
        _primaryButtonBrushSubscription = primaryButtonBrushObservable!.Subscribe(x =>
        {
            PrimaryButtonBrush = x as IBrush;
        });

        RemoveCommand.NotifyCanExecuteChanged();
        InstallCommand.NotifyCanExecuteChanged();
        UpdateCommand.NotifyCanExecuteChanged();
    }

    private async Task ConfirmLicenseAndDownloadAsync(Control? control, IPackageState model, PackageVersion version)
    {
        var topLevel = TopLevel.GetTopLevel(control);

        if (version.IsPrerelease)
        {
            var warningResult = await ContainerLocator.Container!.Resolve<IWindowService>()
                .ShowYesNoAsync("Install Prerelease",
                    "The selected version is a prerelease version. Bugs are expected. Do you want to continue?",
                    MessageBoxIcon.Warning, topLevel as Window);

            if (warningResult != MessageBoxStatus.Yes) return;
        }

        if (model.Package.AcceptLicenseBeforeDownload)
        {
            if (Tabs.FirstOrDefault(x => x.Title == "License") is not { } licenseTab) return;

            var result = await ContainerLocator.Container!.Resolve<IWindowService>()
                .ShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = "Confirm License",
                    Icon = MessageBoxIcon.Info,
                    Message = licenseTab.Content,
                    Buttons =
                    [
                        new MessageBoxButton
                        {
                            Text = "Decline",
                            Role = MessageBoxButtonRole.No,
                            Style = MessageBoxButtonStyle.Secondary,
                            IsDefault = true
                        },
                        new MessageBoxButton
                        {
                            Text = "Accept",
                            Role = MessageBoxButtonRole.Yes,
                            Style = MessageBoxButtonStyle.Primary,
                            IsDefault = true
                        }
                    ]
                }, topLevel as Window);

            if (!result.IsAccepted) return;
        }

        await _packageService.InstallAsync(model.Package.Id!, version);
    }

    private async Task ResolveIconAsync()
    {
        if (_resolveImageStarted) return;
        _resolveImageStarted = true;

        var icon = await _packageService.DownloadPackageIconAsync(PackageState.Package);

        if (icon == null)
        {
            var iconObservable = Application.Current!.GetResourceObservable("BoxIcons.RegularExtension");
            iconObservable.Subscribe(x => { Image = x as IImage; });
        }
        else
        {
            Image = icon;
        }
    }

    public async Task ResolveTabsAsync()
    {
        if (_resolveTabsStarted) return;
        _resolveTabsStarted = true;
        IsTabsResolved = false;
        Tabs.Clear();

        if (PackageState.Package.Tabs != null)
            foreach (var tab in PackageState.Package.Tabs)
            {
                if (tab.ContentUrl == null) continue;
                var content = await _httpService.DownloadTextAsync(tab.ContentUrl);

                Tabs.Add(new TabModel(tab.Title ?? "Title", content ?? "Failed Loading Content"));
            }

        IsTabsResolved = true;
    }

    private async Task CheckSelectedVersionCompatibilityAsync()
    {
        if (SelectedVersionModel == null) return;
        if (PackageState.Package.Id == null) return;

        if (SelectedVersionModel.CompatibilityReport == null)
            SelectedVersionModel.CompatibilityReport =
                await _packageService.CheckCompatibilityAsync(PackageState.Package.Id!, SelectedVersionModel.Version);
    }

    private async Task AskForRestartAsync(Control? owner)
    {
        var ownerWindow = TopLevel.GetTopLevel(owner) as Window;

        var result = await _windowService.ShowYesNoAsync(
            "Restart now?",
            "The changes to this package require a restart to be effective. Do you want to restart now?",
            MessageBoxIcon.Warning, ownerWindow);

        if (result == MessageBoxStatus.Yes)
        {
            ContainerLocator.Container.Resolve<PackageManagerViewModel>().AskForRestart = false;
            _ = _applicationStateService.TryRestartAsync();
        }
    }
}
