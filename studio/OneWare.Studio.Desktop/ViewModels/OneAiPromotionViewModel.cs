using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Studio.Desktop.Views;

namespace OneWare.Studio.Desktop.ViewModels;

/// <summary>
/// Subtle hint below the welcome screen logo while the ONE AI extension is not installed. Hidden once it is
/// installed or the user dismissed it.
/// </summary>
public class OneAiPromotionViewModel : ObservableObject
{
    public const string ShowPromotionKey = "OneAi_ShowWelcomePromotion";
    private const string PackageId = "OneWare.AI";

    private readonly IPackageService _packageService;
    private readonly IPluginService _pluginService;
    private readonly IPackageWindowService _packageWindowService;
    private readonly ISettingsService _settingsService;
    private readonly IPaths _paths;
    private readonly IWindowService _windowService;
    private readonly AiReleaseWindowViewModel _releaseWindowViewModel;

    private IPackageState? _packageState;

    public OneAiPromotionViewModel(IPackageService packageService, IPluginService pluginService,
        IPackageWindowService packageWindowService, ISettingsService settingsService, IPaths paths,
        IWindowService windowService, AiReleaseWindowViewModel releaseWindowViewModel)
    {
        _packageService = packageService;
        _pluginService = pluginService;
        _packageWindowService = packageWindowService;
        _settingsService = settingsService;
        _paths = paths;
        _windowService = windowService;
        _releaseWindowViewModel = releaseWindowViewModel;

        InstallCommand = new AsyncRelayCommand(() => _packageWindowService.QuickInstallPackageAsync(PackageId));
        ShowDetailsCommand = new RelayCommand(() => _windowService.Show(new AiReleaseWindow
        {
            DataContext = _releaseWindowViewModel
        }));
        DismissCommand = new RelayCommand(Dismiss);

        // Raised from background threads.
        _packageService.PackagesUpdated += (_, _) => Dispatcher.UIThread.Post(Update);
        Update();
    }

    public AsyncRelayCommand InstallCommand { get; }
    public RelayCommand ShowDetailsCommand { get; }
    public RelayCommand DismissCommand { get; }

    public bool IsVisible
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool IsInstalling
    {
        get;
        private set => SetProperty(ref field, value);
    }

    private void Dismiss()
    {
        _settingsService.SetSettingValue(ShowPromotionKey, false);
        _settingsService.Save(_paths.SettingsPath);
        Update();
    }

    private void Update()
    {
        var state = _packageService.Packages.GetValueOrDefault(PackageId);
        if (!ReferenceEquals(state, _packageState))
        {
            if (_packageState != null) _packageState.PropertyChanged -= OnPackageStateChanged;
            _packageState = state;
            if (_packageState != null) _packageState.PropertyChanged += OnPackageStateChanged;
        }

        var status = state?.Status;
        IsInstalling = status == PackageStatus.Installing;

        var installed = _pluginService.InstalledPlugins.Any(x => x.Id == PackageId) ||
                        status is PackageStatus.Installed or PackageStatus.NeedRestart
                            or PackageStatus.UpdateAvailable or PackageStatus.UpdateAvailablePrerelease;

        IsVisible = !installed && _settingsService.GetSettingValue<bool>(ShowPromotionKey);
    }

    private void OnPackageStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IPackageState.Status) or null)
            Dispatcher.UIThread.Post(Update);
    }
}
