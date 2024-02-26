using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.PackageManager.Enums;
using OneWare.PackageManager.Models;
using OneWare.PackageManager.Serializer;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.ViewModels;

public abstract class PackageViewModel : ObservableObject
{
    private readonly IHttpService _httpService;
    private readonly ILogger _logger;
    
    private bool _resolveTabsStarted;
    private bool _resolveImageStarted;

    private bool _isTabsResolved;

    public bool IsTabsResolved
    {
        get => _isTabsResolved;
        set => SetProperty(ref _isTabsResolved, value);
    }

    private Package _package;
    public Package Package
    {
        get => _package;
        set
        {
            SetProperty(ref _package, value);
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

    private PackageVersion? _installedVersion;
    public PackageVersion? InstalledVersion
    {
        get => _installedVersion;
        set => SetProperty(ref _installedVersion, value);
    }

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

    private PackageStatus _status;
    public PackageStatus Status
    {
        get => _status;
        set
        {
            SetProperty(ref _status, value);
            UpdateStatus();
        }
    }

    private float _progress;
    public float Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
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

    private bool _primaryButtonEnabled;
    public bool PrimaryButtonEnabled
    {
        get => _primaryButtonEnabled;
        private set => SetProperty(ref _primaryButtonEnabled, value);
    }

    private string? _warningText;
    public string? WarningText
    {
        get => _warningText;
        set => SetProperty(ref _warningText, value);
    }

    protected string ExtractionFolder { get; }

    protected string PackageType { get; }

    public event EventHandler? Installed;

    public event EventHandler? Removed;

    public AsyncRelayCommand RemoveCommand { get; }

    public AsyncRelayCommand InstallCommand { get; }
    
    public AsyncRelayCommand UpdateCommand { get; }
    

    protected PackageViewModel(Package package, string packageType, string extractionFolder, IHttpService httpService, ILogger logger)
    {
        _package = package;
        _httpService = httpService;
        _logger = logger;

        ExtractionFolder = extractionFolder;
        PackageType = packageType;

        RemoveCommand = new AsyncRelayCommand(RemoveAsync, () => Status is PackageStatus.Installed or PackageStatus.UpdateAvailable);
        
        InstallCommand = new AsyncRelayCommand(DownloadAsync, () => false);
        
        UpdateCommand = new AsyncRelayCommand(UpdateAsync, () => false);
        
        InitPackage();
    }

    private void InitPackage()
    {
        Links.Clear();
        if(Package.Links != null) Links.AddRange(Package.Links.Select(x => new LinkModel(x.Name ?? "Link", x.Url ?? "")));
        SelectedVersion = Package.Versions?.LastOrDefault();
        _resolveTabsStarted = false;
        _resolveImageStarted = false;
        UpdateStatus();
        _ = ResolveIconAsync();
    }

    private void UpdateStatus()
    {
        if(Status is not PackageStatus.Installing 
           && Version.TryParse(SelectedVersion?.Version ?? "", out var sV) 
           && Version.TryParse(InstalledVersion?.Version ?? "", out var iV))
        {
            if (Status is PackageStatus.Installed && sV > iV)
            {
                Status = PackageStatus.UpdateAvailable;
                return;
            }
            if (Status is PackageStatus.UpdateAvailable && sV <= iV)
            {
                Status = PackageStatus.Installed;
                return;
            }
        }

        PrimaryButtonText = Status switch
        {
            PackageStatus.Available => "Install",
            PackageStatus.UpdateAvailable => "Update",
            PackageStatus.Installed => "Remove",
            PackageStatus.Installing => "Installing...",
            PackageStatus.Unavailable => "Unavailable",
            PackageStatus.NeedRestart => "Restart Required",
            _ => "Unknown"
        };
            
        var primaryButtonBrushObservable = Status switch
        {
            PackageStatus.Available => Application.Current!.GetResourceObservable("ThemeAccentBrush"),
            PackageStatus.UpdateAvailable => Application.Current!.GetResourceObservable("ThemeAccentBrush"),
            _ => Application.Current!.GetResourceObservable("ThemeBorderMidBrush")
        };
            
        _primaryButtonBrushSubscription?.Dispose();
        _primaryButtonBrushSubscription = primaryButtonBrushObservable!.Subscribe(x =>
        {
            PrimaryButtonBrush = x as IBrush;
        });
            
        PrimaryButtonEnabled = Status is PackageStatus.Available or PackageStatus.Installed or PackageStatus.UpdateAvailable;
        RemoveCommand.NotifyCanExecuteChanged();
    }
    
    public Task ExecuteMainButtonAsync()
    {
        switch (Status)
        {
            case PackageStatus.UpdateAvailable:
                return UpdateAsync();
            case PackageStatus.Available:
                return DownloadAsync(); 
            case PackageStatus.Installed:
                return RemoveAsync();
        }

        return Task.CompletedTask;
    }

    private async Task ResolveIconAsync()
    {
        if (_resolveImageStarted) return;
        _resolveImageStarted = true;
        
        var icon = Package.IconUrl != null
       ? await _httpService.DownloadImageAsync(Package.IconUrl)
       : null;

        Image = icon;
    }

    public async Task ResolveTabsAsync()
    {
        if (_resolveTabsStarted) return;
        _resolveTabsStarted = true;
        IsTabsResolved = false;
        Tabs.Clear();
        
        if (Package.Tabs != null)
        {
            foreach (var tab in Package.Tabs)
            {
                if(tab.ContentUrl == null) continue;
                var content = await _httpService.DownloadTextAsync(tab.ContentUrl);
                                
                Tabs.Add(new TabModel(tab.Title ?? "Title", content ?? "Failed Loading Content"));
            }
        }
        
        IsTabsResolved = true;
    }

    private async Task UpdateAsync()
    {
        await RemoveAsync();
        await DownloadAsync();
    }
    
    private async Task DownloadAsync()
    {
        try
        {
            Status = PackageStatus.Installing;
            
            var currentTarget = PlatformHelper.Platform.ToString().ToLower();

            var selectedVersion = SelectedVersion;
            
            var target = Package.Versions?
                .FirstOrDefault(x => x == selectedVersion)?
                .Targets?.FirstOrDefault(x => x.Target?.Replace("-", "") == currentTarget);

            if (target is {Url: not null})
            {
                var progress = new Progress<float>(x =>
                {
                    Progress = x;
                });
                
                //Download
                if (!await _httpService.DownloadAndExtractArchiveAsync(target.Url, ExtractionFolder, progress))
                {
                    Status = PackageStatus.Available;
                    return;
                }
                
                Install();
                
                InstalledVersion = selectedVersion;
                
                Installed?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                throw new NotSupportedException("Target not found!");
            }
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            Status = PackageStatus.Available;
        }
    }

    /// <summary>
    /// Gets called after downloading and extracting
    /// Make sure to set Status after completing
    /// </summary>
    protected abstract void Install();

    /// <summary>
    /// Gets called after deleting the package
    /// Make sure to set Status after completing
    /// </summary>
    protected abstract void Uninstall();

    private Task RemoveAsync()
    {
        if (Package.Id == null) throw new NullReferenceException(nameof(Package.Id));

        if (Directory.Exists(ExtractionFolder))
        {
            try
            {
                Directory.Delete(ExtractionFolder, true);
            }
            catch (Exception e)
            {
                _logger.Error(e.Message, e);
                return Task.CompletedTask;
            }
        }
        
        InstalledVersion = null;
        
        Uninstall();
        
        Removed?.Invoke(this, EventArgs.Empty);
        
        return Task.CompletedTask;
    }
}