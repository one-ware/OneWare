using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.PackageManager.Enums;
using OneWare.PackageManager.Models;
using OneWare.PackageManager.Serializer;
using OneWare.Shared.Helpers;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using Prism.Modularity;

namespace OneWare.PackageManager.ViewModels;

public class PackageViewModel : ObservableObject
{
    private readonly IHttpService _httpService;
    private readonly IPaths _paths;
    private readonly ILogger _logger;
    private readonly IPluginService _pluginService;
    
    public Package Package { get; }
    public IImage? Image { get; private set; }
    public List<TabModel>? Tabs { get; private set; }
    public List<LinkModel>? Links { get; private set; }

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

    private bool _primaryButtonEnabled = false;
    public bool PrimaryButtonEnabled
    {
        get => _primaryButtonEnabled;
        set => SetProperty(ref _primaryButtonEnabled, value);
    }

    public PackageViewModel(Package package, IHttpService httpService, IPaths paths, ILogger logger, IPluginService pluginService)
    {
        Package = package;
        _httpService = httpService;
        _paths = paths;
        _logger = logger;
        _pluginService = pluginService;

        if (_pluginService.InstalledPlugins.Any(x => package.Id == x))
        {
            Status = PackageStatus.Installed;
        }
        else
        {
            Status = PackageStatus.Available;
        }
    }

    public async Task ExecuteMainButtonAsync()
    {
        switch (Status)
        {
            case PackageStatus.Available:
                await InstallAsync(); 
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

    private async Task InstallAsync()
    {
        try
        {
            Status = PackageStatus.Installing;
            
            var currentTarget = PlatformHelper.Platform.ToString().ToLower();
        
            var target = Package.Versions?
                .FirstOrDefault(x => x == SelectedVersion)?
                .Targets?.FirstOrDefault(x => x.Target?.Replace("-", "") == currentTarget);

            if (target is {Url: not null})
            {
                var progress = new Progress<float>(x =>
                {
                    Progress = x;
                });

                var path = Path.Combine(_paths.ModulesPath, Package!.Id!);
                
                //Download
                await _httpService.DownloadAndExtractArchiveAsync(target.Url, path, progress);
                
                _pluginService.AddPlugin(path);
                Status = PackageStatus.Installed;
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

    public async Task RemoveAsync()
    {
        if (Package.Id == null) throw new NullReferenceException(nameof(Package.Id));
        _pluginService.RemovePlugin(Package.Id!);
        Status = PackageStatus.Available;
        PrimaryButtonEnabled = false;
        PrimaryButtonText = "Restart Required";
    }
}