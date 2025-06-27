using System.Collections.ObjectModel;
using System.Reflection;
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
using Prism.Ioc;

namespace OneWare.PackageManager.ViewModels;

public class PackageViewModel : ObservableObject
{
    private readonly IHttpService _httpService;

    private IImage? _image;

    private bool _isTabsResolved;

    private AsyncRelayCommand<Control?>? _mainButtonCommand;

    private PackageModel _packageModel;

    private IBrush? _primaryButtonBrush;

    private IDisposable? _primaryButtonBrushSubscription;

    private string _primaryButtonText = string.Empty;
    private bool _resolveImageStarted;

    private bool _resolveTabsStarted;

    private PackageVersionModel? _selectedVersionModel;

    protected PackageViewModel(PackageModel packageModel, IHttpService httpService)
    {
        _packageModel = packageModel;
        _httpService = httpService;                     

        RemoveCommand = new AsyncRelayCommand<Control?>(x => PackageModel.RemoveAsync(),
            x => PackageModel.Status is PackageStatus.Installed or PackageStatus.UpdateAvailable);

        InstallCommand = new AsyncRelayCommand<Control?>(x => ConfirmLicenseAndDownloadAsync(x, PackageModel, SelectedVersionModel!.Version),
            x => PackageModel.Status is PackageStatus.Available);

        UpdateCommand = new AsyncRelayCommand<Control?>(x => PackageModel.UpdateAsync(SelectedVersionModel!.Version),
            x => PackageModel.Status is PackageStatus.UpdateAvailable);

        PackageModel.WhenValueChanged(x => x.Status).Subscribe(_ => UpdateStatus());
        InitPackage();
    }
    
    public bool IsTabsResolved
    {
        get => _isTabsResolved;
        set => SetProperty(ref _isTabsResolved, value);
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
        get => _image;
        private set => SetProperty(ref _image, value);
    }

    public ObservableCollection<PackageVersionModel> PackageVersionModels { get; } = new();
    public ObservableCollection<TabModel> Tabs { get; } = [];
    public ObservableCollection<LinkModel> Links { get; } = [];

    public PackageVersionModel? SelectedVersionModel
    {
        get => _selectedVersionModel;
        set
        {
            SetProperty(ref _selectedVersionModel, value);
            UpdateStatus();
            _ = CheckSelectedVersionCompatibilityAsync();
        }
    }

    public string PrimaryButtonText
    {
        get => _primaryButtonText;
        private set => SetProperty(ref _primaryButtonText, value);
    }

    public IBrush? PrimaryButtonBrush
    {
        get => _primaryButtonBrush;
        private set => SetProperty(ref _primaryButtonBrush, value);
    }

    public AsyncRelayCommand<Control?>? MainButtonCommand
    {
        get => _mainButtonCommand;
        set => SetProperty(ref _mainButtonCommand, value);
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
        
        SelectedVersionModel = PackageVersionModels.FirstOrDefault(x => x.Version.MinStudioVersion == null || Version.TryParse(x.Version.MinStudioVersion, out var minVersion) 
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
                PrimaryButtonText = "Update";
                primaryButtonBrushObservable = Application.Current!.GetResourceObservable("ThemeAccentBrush");
                MainButtonCommand = UpdateCommand;
                break;
            case PackageStatus.Installed:
            case PackageStatus.UpdateAvailable:
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
                MainButtonCommand = new AsyncRelayCommand<Control?>(x => Task.CompletedTask, x => false);
                primaryButtonBrushObservable = Application.Current!.GetResourceObservable("ThemeControlLowBrush");
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
        if (model.Package.AcceptLicenseBeforeDownload)
        {
            if (Tabs.FirstOrDefault(x => x.Title == "License") is not { } licenseTab) return;
            
            var topLevel = TopLevel.GetTopLevel(control);

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

    public async Task CheckSelectedVersionCompatibilityAsync()
    {
        if(SelectedVersionModel == null) return;

        if(SelectedVersionModel.CompatibilityReport == null)
            SelectedVersionModel.CompatibilityReport = await PackageModel.CheckCompatibilityAsync(SelectedVersionModel.Version);
    }
}