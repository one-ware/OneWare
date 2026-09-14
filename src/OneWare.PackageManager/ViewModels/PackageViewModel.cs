using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Models;

namespace OneWare.PackageManager.ViewModels;

public sealed record PackageDependencyViewModel(string Id, string Requirement, string Status, bool CanOpen);

public class PackageViewModel : PackageListEntryViewModel, IDisposable
{
    private readonly IHttpService _httpService;
    private readonly IPackageService _packageService;
    private readonly IWindowService _windowService;
    private readonly IApplicationStateService _applicationStateService;
    private readonly ILogger _logger;
    
    private IPackageState _packageState;
    private Package? _manifest;
    private PackageVersion[]? _manifestVersions;

    private IDisposable? _primaryButtonBrushSubscription;
    private IDisposable? _statusSubscription;
    private IDisposable? _iconSubscription;
    private int _generation;
    private bool _disposed;
    public void Dispose()
    {
        _disposed = true;
        _generation++;
        _primaryButtonBrushSubscription?.Dispose();
        _statusSubscription?.Dispose();
        _iconSubscription?.Dispose();
    }

    private bool _resolveImageStarted;

    private Task? _tabsTask;

    public PackageViewModel(IPackageState packageState, IPackageService packageService, IHttpService httpService,
        IWindowService windowService, IApplicationStateService applicationStateService, ILogger logger)
    {
        _packageState = packageState;
        _packageService = packageService;
        _httpService = httpService;
        _windowService = windowService;
        _applicationStateService = applicationStateService;
        _logger = logger;

        ResolveIconCommand = new AsyncRelayCommand(ResolveIconAsync);

        RemoveCommand = new AsyncRelayCommand<Control?>(ConfirmRemoveAsync,
            _ => PackageState.InstalledVersion != null && PackageState.Status is not (PackageStatus.Installing or PackageStatus.NeedRestart));

        InstallCommand = new AsyncRelayCommand<Control?>(
            x => ConfirmLicenseAndDownloadAsync(x),
            _ => SelectedVersionModel != null && PackageState.Status is PackageStatus.Available);

        UpdateCommand = new AsyncRelayCommand<Control?>(ConfirmLicenseAndDownloadAsync,
            _ => SelectedVersionModel != null && PackageState.Status is PackageStatus.UpdateAvailable or PackageStatus.UpdateAvailablePrerelease);

        CancelCommand = new RelayCommand(() => _packageService.CancelInstall(PackageState.Package.Id!),
            () => PackageState.Status is PackageStatus.Installing);

        SubscribeStatus();
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
            if (!SetProperty(ref _packageState, value)) return;
            SubscribeStatus();
            InitPackage();
        }
    }

    public IImage? Image
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public ObservableCollection<PackageDependencyViewModel> Dependencies { get; } = [];
    public string DependencySummary => Dependencies.Count > 0 ? "Required by the selected version:" :
        PackageState.InstalledVersion != null &&
        (SelectedVersionModel?.Version ?? PackageState.InstalledVersion) is { Dependencies: null, Targets: null } version &&
        version.Version == PackageState.InstalledVersion.Version
            ? "Dependency metadata is unavailable for this installed version." : "No declared dependencies.";
    public string? LatestVersion => PackageVersionModels.FirstOrDefault()?.Version.Version;
    public bool IsInstalling => PackageState.Status == PackageStatus.Installing;
    public bool IsCompatibilityChecking { get; private set => SetProperty(ref field, value); }
    public string? OperationMessage { get; private set => SetProperty(ref field, value); }

    public void RefreshDependencies()
    {
        var rows = new List<PackageDependencyViewModel>();
        foreach (var dependency in (SelectedVersionModel?.Version ?? PackageState.InstalledVersion)?.Dependencies ?? [])
        {
            var state = _packageService.Packages.GetValueOrDefault(dependency.Id);
            var version = state?.InstalledVersion?.Version;
            string status;
            try
            {
                status = version == null ? (state == null ? "Not in current catalog" : "Not installed") :
                    $"Installed {version} — {(dependency.Accepts(version) ? "satisfies requirement" : "version change required")}";
            }
            catch (InvalidOperationException) { status = "Invalid dependency requirement"; }
            var requirement = dependency.MinVersion == null && dependency.MaxVersionExclusive == null ? "Any version" :
                string.Join(", ", new[] { dependency.MinVersion == null ? null : $"≥ {dependency.MinVersion}",
                    dependency.MaxVersionExclusive == null ? null : $"< {dependency.MaxVersionExclusive}" }.OfType<string>());
            rows.Add(new(dependency.Id, requirement, status, state != null));
        }
        if (!Dependencies.SequenceEqual(rows))
        {
            Dependencies.Clear();
            foreach (var row in rows) Dependencies.Add(row);
        }
        OnPropertyChanged(nameof(DependencySummary));
    }

    private void SubscribeStatus()
    {
        _statusSubscription?.Dispose();
        _statusSubscription = PackageState.WhenValueChanged(x => x.Status).Subscribe(_ =>
        {
            if (Dispatcher.UIThread.CheckAccess()) UpdateStatus();
            else Dispatcher.UIThread.Post(() => { if (!_disposed) UpdateStatus(); });
        });
    }

    public ObservableCollection<PackageVersionModel> PackageVersionModels { get; } = new();
    public ObservableCollection<TabModel> Tabs { get; } = [];
    public ObservableCollection<LinkModel> Links { get; } = [];

    public PackageVersionModel? SelectedVersionModel
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;
            UpdateStatus();
            RefreshDependencies();
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

    public ICommand CancelCommand { get; }

    /// <summary>
    /// Resolves the package icon lazily, invoked when the package entry enters the visual tree.
    /// </summary>
    public ICommand ResolveIconCommand { get; }

    public void RefreshMetadata()
    {
        if (!ReferenceEquals(_manifest, PackageState.Package) || !ReferenceEquals(_manifestVersions, PackageState.Package.Versions))
            InitPackage();
    }

    private void InitPackage()
    {
        _manifest = PackageState.Package;
        _manifestVersions = _manifest.Versions;
        _generation++;
        var tabsWereRequested = _tabsTask != null;
        _tabsTask = null;
        Tabs.Clear();
        IsTabsResolved = false;
        Links.Clear();
        if (PackageState.Package.Links != null)
            Links.AddRange(PackageState.Package.Links.Select(x => new LinkModel(x.Name ?? "Link", x.Url ?? "")));

        PackageVersionModels.Clear();
        if (PackageState.Package.Versions != null)
            PackageVersionModels.AddRange(PackageState.Package.Versions
                .OrderByDescending(x =>
                {
                    if (SemanticVersion.TryParse(x.Version, out var v)) return v;
                    return SemanticVersion.Empty;
                })
                .Select(x => new PackageVersionModel(x)));

        var target = PackageState.ResolveTargetVersion();

        SelectedVersionModel = PackageVersionModels.FirstOrDefault(x => x.Version == target);

        OnPropertyChanged(nameof(LatestVersion));
        RefreshDependencies();

        var iconWasRequested = _resolveImageStarted;
        _resolveImageStarted = false;

        UpdateStatus();

        // Only reload the icon if it was requested before, icons are resolved lazily when the package becomes visible.
        if (iconWasRequested) _ = ResolveIconAsync();
        if (tabsWereRequested) _ = ResolveTabsAsync();
    }

    private void UpdateStatus()
    {
        if (_disposed) return;
        OnPropertyChanged(nameof(IsInstalling));
        SemanticVersion.TryParse(SelectedVersionModel?.Version.Version, out var sV);
        SemanticVersion.TryParse(PackageState.InstalledVersion?.Version, out var iV);

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
                PrimaryButtonText = "Installed";
                break;
            case PackageStatus.Installing:
                PrimaryButtonText = "Cancel";
                MainButtonCommand = CancelCommand;
                primaryButtonBrushObservable = Application.Current!.GetResourceObservable("ThemeControlMidBrush");
                break;
            case PackageStatus.Unavailable when PackageState.InstalledVersion != null:
                PrimaryButtonText = "Installed";
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
        (CancelCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private async Task ConfirmLicenseAndDownloadAsync(Control? control)
    {
        if (SelectedVersionModel is not { } selected) return;
        OperationMessage = null;
        var result = await PackageOperationReview.RunAsync(_packageService, _windowService,
            [new(PackageState.Package.Id!, selected.Version.Version, selected.Version.IsPrerelease)], control == null ? null : TopLevel.GetTopLevel(control) as Window);
        OperationMessage = PackageOperationReview.DescribeResult(result);
    }

    private async Task ConfirmRemoveAsync(Control? control)
    {
        var owner = control == null ? null : TopLevel.GetTopLevel(control) as Window;
        if (await _windowService.ShowYesNoAsync("Remove package", $"Remove {PackageState.Package.Name}? Dependencies will be retained.",
                MessageBoxIcon.Warning, owner) != MessageBoxStatus.Yes) return;
        try
        {
            if (await _packageService.RemoveAsync(PackageState.Package.Id!)) return;
            OperationMessage = "Another installed package requires this package, or removal failed. See the log for details.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove package {PackageId}", PackageState.Package.Id);
            OperationMessage = "Removal failed. " + ex.Message;
        }
        await _windowService.ShowMessageAsync("Cannot remove package", OperationMessage!, MessageBoxIcon.Warning, owner);
    }

    /// <summary>
    /// Resolves the package icon. Called when the package becomes visible in the package list.
    /// </summary>
    public async Task ResolveIconAsync()
    {
        if (_resolveImageStarted) return;
        _resolveImageStarted = true;
        var generation = _generation;
        _iconSubscription?.Dispose();
        _iconSubscription = null;

        IImage? icon = null;

        try
        {
            icon = await _packageService.DownloadPackageIconAsync(PackageState.Package);
        }
        catch (Exception e)
        {
            // Failing icons are not critical and must never be reported to the user
            _logger.LogDebug(e, "Failed to resolve icon for package {PackageId}", PackageState.Package.Id);
        }

        if (_disposed || generation != _generation) return;
        if (icon == null)
        {
            var iconObservable = Application.Current!.GetResourceObservable("BoxIcons.RegularExtension");
            _iconSubscription?.Dispose();
            _iconSubscription = iconObservable.Subscribe(x => { Image = x as IImage; });
        }
        else
        {
            Image = icon;
        }
    }

    public Task ResolveTabsAsync() => _tabsTask ??= LoadTabsAsync(_generation, PackageState.Package);

    private async Task LoadTabsAsync(int generation, Package package)
    {
        IsTabsResolved = false;
        Tabs.Clear();
        Tabs.Add(new TabModel("About", package.Description ?? "No description provided."));

        if (package.Tabs != null)
            foreach (var tab in package.Tabs)
            {
                if (tab.ContentUrl == null) continue;
                string? content;
                try { content = await _httpService.DownloadTextAsync(tab.ContentUrl); }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to load package tab {Title}", tab.Title);
                    content = null;
                }
                if (_disposed || generation != _generation) return;
                Tabs.Add(new TabModel(tab.Title ?? "Details", content ?? "Content unavailable. Refresh the package sources to retry."));
            }

        if (_disposed || generation != _generation) return;
        if (!Tabs.Any(x => x.Title.Equals("License", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(package.License))
            Tabs.Add(new TabModel("License", package.License));
        IsTabsResolved = true;
    }

    private async Task CheckSelectedVersionCompatibilityAsync()
    {
        var selected = SelectedVersionModel;
        var id = PackageState.Package.Id;
        IsCompatibilityChecking = selected != null && selected.CompatibilityReport == null;
        if (selected == null || id == null || selected.CompatibilityReport != null) return;
        try { selected.CompatibilityReport = await _packageService.CheckCompatibilityAsync(id, selected.Version); }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not preview package compatibility for {PackageId}", id);
            // The service performs mandatory compatibility validation again before installation.
            if (ReferenceEquals(selected, SelectedVersionModel)) OperationMessage = "Compatibility preview unavailable. Installation will validate compatibility before making changes.";
        }
        finally
        {
            if (ReferenceEquals(selected, SelectedVersionModel)) IsCompatibilityChecking = false;
        }
    }

    private async Task AskForRestartAsync(Control? owner)
    {
        var ownerWindow = owner == null ? null : TopLevel.GetTopLevel(owner) as Window;

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
