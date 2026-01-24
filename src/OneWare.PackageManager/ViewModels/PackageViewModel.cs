using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    
    private readonly IApplicationStateService _applicationStateService;

    private readonly IWindowService _windowService;

    private PackageModel _packageModel;

    private IDisposable? _primaryButtonBrushSubscription;

    private bool _resolveImageStarted;

    private bool _resolveTabsStarted;

    protected PackageViewModel(PackageModel packageModel, IHttpService httpService, IApplicationStateService applicationStateService, IWindowService windowService)
    {
        _packageModel = packageModel;
        _httpService = httpService;
        _applicationStateService = applicationStateService;
        _windowService = windowService;

        RemoveCommand = new AsyncRelayCommand<Control?>(x => PackageModel.RemoveAsync(),
            x => PackageModel.Status is PackageStatus.Installed or PackageStatus.UpdateAvailable or PackageStatus.UpdateAvailablePrerelease);

        InstallCommand = new AsyncRelayCommand<Control?>(
            x => ConfirmLicenseAndDownloadAsync(x, PackageModel, SelectedVersionModel!.Version),
            x => PackageModel.Status is PackageStatus.Available);

        UpdateCommand = new AsyncRelayCommand<Control?>(x => PackageModel.UpdateAsync(SelectedVersionModel!.Version),
            x => PackageModel.Status is PackageStatus.UpdateAvailable or PackageStatus.UpdateAvailablePrerelease);

        PackageModel.WhenValueChanged(x => x.Status).Subscribe(_ => UpdateStatus());
        InitPackage();
    }

    public bool IsTabsResolved
    {
        get;
        set => SetProperty(ref field, value);
    }

    public PackageModel PackageModel
    {
        get => _packageModel;
        set
        {
            SetProperty(ref _packageModel, value);
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
        if (PackageModel.Package.Links != null)
            Links.AddRange(PackageModel.Package.Links.Select(x => new LinkModel(x.Name ?? "Link", x.Url ?? "")));

        PackageVersionModels.Clear();
        if (PackageModel.Package.Versions != null)
            PackageVersionModels.AddRange(PackageModel.Package.Versions
                .OrderByDescending(x =>
                {
                    if (Version.TryParse(x.Version, out var v)) return v;
                    return new Version(int.MaxValue, 0);
                })
                .Select(x => new PackageVersionModel(x)));

        var includePrerelease = PackageModel.InstalledVersion?.IsPrerelease ?? false;

        SelectedVersionModel = PackageVersionModels.OrderBy(x => includePrerelease || x.Version.IsPrerelease)
            .FirstOrDefault(x => x.Version.MinStudioVersion == null
                                 || Version.TryParse(x.Version.MinStudioVersion, out var minVersion)
                                 && Assembly.GetEntryAssembly()!.GetName().Version >= minVersion);

        _resolveTabsStarted = false;
        _resolveImageStarted = false;
        UpdateStatus();
        _ = ResolveIconAsync();
    }

    private void UpdateStatus()
    {
        Version.TryParse(SelectedVersionModel?.Version.Version ?? "", out var sV);
        Version.TryParse(PackageModel.InstalledVersion?.Version ?? "", out var iV);

        MainButtonCommand = null;
        var primaryButtonBrushObservable = Application.Current!.GetResourceObservable("ThemeBorderMidBrush");
        switch (PackageModel.Status)
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

    private async Task ConfirmLicenseAndDownloadAsync(Control? control, PackageModel model, PackageVersion version)
    {
        var topLevel = TopLevel.GetTopLevel(control);

        if (version.IsPrerelease)
        {
            var warningResult = await ContainerLocator.Container.Resolve<IWindowService>()
                .ShowYesNoAsync("Install Prerelease",
                    "The selected version is a prerelease version. Bugs are expected. Do you want to continue?",
                    MessageBoxIcon.Warning, topLevel as Window);

            if (warningResult != MessageBoxStatus.Yes) return;
        }

        if (model.Package.AcceptLicenseBeforeDownload)
        {
            if (Tabs.FirstOrDefault(x => x.Title == "License") is not { } licenseTab) return;

            var result = await ContainerLocator.Container.Resolve<IWindowService>()
                .ShowYesNoAsync("Confirm License", licenseTab.Content, MessageBoxIcon.Info, topLevel as Window);

            if (result != MessageBoxStatus.Yes) return;
        }

        await _packageModel.DownloadAsync(version);
    }

    private async Task ResolveIconAsync()
    {
        if (_resolveImageStarted) return;
        _resolveImageStarted = true;

        var icon = PackageModel.Package.IconUrl != null
            ? await _httpService.DownloadImageAsync(PackageModel.Package.IconUrl)
            : null;

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

        if (PackageModel.Package.Tabs != null)
            foreach (var tab in PackageModel.Package.Tabs)
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

        if (SelectedVersionModel.CompatibilityReport == null)
            SelectedVersionModel.CompatibilityReport =
                await PackageModel.CheckCompatibilityAsync(SelectedVersionModel.Version);
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