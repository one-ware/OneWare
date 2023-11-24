using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.PackageManager.Enums;
using OneWare.PackageManager.Models;
using OneWare.PackageManager.Serializer;
using OneWare.SDK.Helpers;
using OneWare.SDK.Services;

namespace OneWare.PackageManager.ViewModels;

public abstract class PackageViewModel : ObservableObject
{
    private readonly IHttpService _httpService;
    private readonly IPaths _paths;
    private readonly ILogger _logger;
    
    public Package Package { get; }
    public IImage? Image { get; private set; }
    public List<TabModel>? Tabs { get; private set; }
    public List<LinkModel>? Links { get; private set; }

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
        set => SetProperty(ref _selectedVersion, value);
    }

    private PackageStatus _status;
    public PackageStatus Status
    {
        get => _status;
        set
        {
            SetProperty(ref _status, value);
            PrimaryButtonText = value switch
            {
                PackageStatus.Available => "Install",
                PackageStatus.UpdateAvailable => "Update",
                PackageStatus.Installed => "Remove",
                PackageStatus.Installing => "Cancel",
                PackageStatus.Unavailable => "Unavailable",
                _ => "Unknown"
            };
            
            var primaryButtonBrushObservable = value switch
            {
                PackageStatus.Available => Application.Current!.GetResourceObservable("ThemeAccentBrush"),
                _ => Application.Current!.GetResourceObservable("ThemeBorderMidBrush")
            };
            
            _primaryButtonBrushSubscription?.Dispose();
            _primaryButtonBrushSubscription = primaryButtonBrushObservable!.Subscribe(x =>
            {
                PrimaryButtonBrush = x as IBrush;
            });
            
            PrimaryButtonEnabled = value is PackageStatus.Available or PackageStatus.Installed;
        }
    }

    private float _progress;
    public float Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    private string _primaryButtonText = string.Empty;
    public string PrimaryButtonText
    {
        get => _primaryButtonText;
        set => SetProperty(ref _primaryButtonText, value);
    }

    private IDisposable? _primaryButtonBrushSubscription;
    
    private IBrush? _primaryButtonBrush;
    public IBrush? PrimaryButtonBrush
    {
        get => _primaryButtonBrush;
        set => SetProperty(ref _primaryButtonBrush, value);
    }

    private bool _primaryButtonEnabled;
    public bool PrimaryButtonEnabled
    {
        get => _primaryButtonEnabled;
        set => SetProperty(ref _primaryButtonEnabled, value);
    }

    private string? _warningText;
    public string? WarningText
    {
        get => _warningText;
        set => SetProperty(ref _warningText, value);
    }
    
    protected string ExtractionFolder { get; init; }
    
    protected string PackageType { get; init; }

    public event EventHandler? Installed;

    public event EventHandler? Removed;

    protected PackageViewModel(Package package, IHttpService httpService, IPaths paths, ILogger logger)
    {
        Package = package;
        _httpService = httpService;
        _paths = paths;
        _logger = logger;

        PackageType = "Package";
        ExtractionFolder = paths.PackagesDirectory;
    }

    public async Task ExecuteMainButtonAsync()
    {
        switch (Status)
        {
            case PackageStatus.Available:
            case PackageStatus.UpdateAvailable:
                await DownloadAsync(); 
                break;
            case PackageStatus.Installed:
                await RemoveAsync();
                break;
        }
    }
    
    public async Task ResolveAsync(CancellationToken cancellationToken)
    {
        var icon = Package.IconUrl != null
            ? await _httpService.DownloadImageAsync(Package.IconUrl, cancellationToken: cancellationToken)
            : null;

        var tabs = new List<TabModel>();
        if (Package.Tabs != null)
        {
            foreach (var tab in Package.Tabs)
            {
                if(tab.ContentUrl == null) continue;
                var content = await _httpService.DownloadTextAsync(tab.ContentUrl,
                    cancellationToken: cancellationToken);
                                
                tabs.Add(new TabModel(tab.Title ?? "Title", content ?? "Failed Loading Content"));
            }
        }

        Image = icon;
        Tabs = tabs;
        Links = Package.Links?.Select(x => new LinkModel(x.Name ?? "Link", x.Url ?? "")).ToList();
        SelectedVersion = Package.Versions?.LastOrDefault();
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

                var path = Path.Combine(ExtractionFolder, Package!.Id!);
                
                //Download
                if (!await _httpService.DownloadAndExtractArchiveAsync(target.Url, path, progress))
                {
                    Status = PackageStatus.Available;
                    return;
                }
                
                Install(path);
                
                InstalledVersion = selectedVersion;

                Status = PackageStatus.Installed;
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

    protected abstract void Install(string path);

    protected abstract void Uninstall();

    private Task RemoveAsync()
    {
        if (Package.Id == null) throw new NullReferenceException(nameof(Package.Id));
        
        try
        {
            Directory.Delete(Path.Combine(ExtractionFolder, Package.Id), true);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
        Uninstall();
        
        Status = PackageStatus.Available;
        Removed?.Invoke(this, EventArgs.Empty);
        
        return Task.CompletedTask;
    }
}