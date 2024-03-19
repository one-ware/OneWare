using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using ImTools;
using OneWare.Essentials.Enums;
using OneWare.PackageManager.Models;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Packages;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.ViewModels;

public class PackageViewModel : ObservableObject
{
    private readonly IHttpService _httpService;
    
    private bool _resolveTabsStarted;
    private bool _resolveImageStarted;

    private bool _isTabsResolved;

    public bool IsTabsResolved
    {
        get => _isTabsResolved;
        set => SetProperty(ref _isTabsResolved, value);
    }

    private PackageModel _packageModel;
    public PackageModel PackageModel
    {
        get => _packageModel;
        set
        {
            SetProperty(ref _packageModel, value);
            InitPackage();
        }
    }

    private IImage? _image;
    public IImage? Image
    {
        get => _image;
        private set => SetProperty(ref _image, value);
    }

    public ObservableCollection<TabModel> Tabs { get; } = [];
    public ObservableCollection<LinkModel> Links { get; } = [];

    private PackageVersion? _selectedVersion;
    public PackageVersion? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            SetProperty(ref _selectedVersion, value);
            UpdateStatus();
        }
    }

    private string _primaryButtonText = string.Empty;
    public string PrimaryButtonText
    {
        get => _primaryButtonText;
        private set => SetProperty(ref _primaryButtonText, value);
    }

    private IDisposable? _primaryButtonBrushSubscription;
    
    private IBrush? _primaryButtonBrush;
    public IBrush? PrimaryButtonBrush
    {
        get => _primaryButtonBrush;
        private set => SetProperty(ref _primaryButtonBrush, value);
    }

    private AsyncRelayCommand? _mainButtonCommand;

    public AsyncRelayCommand? MainButtonCommand
    {
        get => _mainButtonCommand;
        set => SetProperty(ref _mainButtonCommand, value);
    }
    
    public AsyncRelayCommand RemoveCommand { get; }

    public AsyncRelayCommand InstallCommand { get; }
    
    public AsyncRelayCommand UpdateCommand { get; }

    protected PackageViewModel(PackageModel packageModel, IHttpService httpService)
    {
        _packageModel = packageModel;
        _httpService = httpService;

        RemoveCommand = new AsyncRelayCommand(PackageModel.RemoveAsync, () => PackageModel.Status is PackageStatus.Installed or PackageStatus.UpdateAvailable);
        
        InstallCommand = new AsyncRelayCommand(() => PackageModel.DownloadAsync(SelectedVersion!), () => PackageModel.Status is PackageStatus.Available);
        
        UpdateCommand = new AsyncRelayCommand(() => PackageModel.UpdateAsync(SelectedVersion!), () => PackageModel.Status is PackageStatus.UpdateAvailable);

        PackageModel.WhenValueChanged(x => x.Status).Subscribe(_ => UpdateStatus());
        InitPackage();
    }

    private void InitPackage()
    {
        Links.Clear();
        if(PackageModel.Package.Links != null) Links.AddRange(PackageModel.Package.Links.Select(x => new LinkModel(x.Name ?? "Link", x.Url ?? "")));
        SelectedVersion = PackageModel.Package.Versions?.LastOrDefault();
        _resolveTabsStarted = false;
        _resolveImageStarted = false;
        UpdateStatus();
        _ = ResolveIconAsync();
    }

    private void UpdateStatus()
    {
        Version.TryParse(SelectedVersion?.Version ?? "", out var sV);
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
                PrimaryButtonText = "Restart IDE";
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
            iconObservable.Subscribe(x =>
            {
                Image = x as IImage;
            });
        }
        else Image = icon;
    }

    public async Task ResolveTabsAsync()
    {
        if (_resolveTabsStarted) return;
        _resolveTabsStarted = true;
        IsTabsResolved = false;
        Tabs.Clear();
        
        if (PackageModel.Package.Tabs != null)
        {
            foreach (var tab in PackageModel.Package.Tabs)
            {
                if(tab.ContentUrl == null) continue;
                var content = await _httpService.DownloadTextAsync(tab.ContentUrl);
                                
                Tabs.Add(new TabModel(tab.Title ?? "Title", content ?? "Failed Loading Content"));
            }
        }
        
        IsTabsResolved = true;
    }
}