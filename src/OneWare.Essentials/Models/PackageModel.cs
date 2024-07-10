using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.Models;

public abstract class PackageModel : ObservableObject
{
    private PackageVersion? _installedVersion;
    public PackageVersion? InstalledVersion
    {
        get => _installedVersion;
        set
        {
            SetProperty(ref _installedVersion, value);
            UpdateStatus();
        }
    }
    
    private string? _warningText;
    public string? WarningText
    {
        get => _warningText;
        set => SetProperty(ref _warningText, value);
    }
    
    private PackageStatus _status;
    public PackageStatus Status
    {
        get => _status;
        protected set => SetProperty(ref _status, value);
    }
    
    private float _progress;
    private Package _package;
    private readonly IHttpService _httpService;
    private readonly ILogger _logger;
    private readonly IApplicationStateService _applicationStateService;

    protected PackageModel(Package package,
        string packageType,
        string extractionFolder,
        IHttpService httpService,
        ILogger logger,
        IApplicationStateService applicationStateService)
    {
        _package = package;
        _httpService = httpService;
        _logger = logger;
        _applicationStateService = applicationStateService;
        ExtractionFolder = extractionFolder;
        PackageType = packageType;
        
        UpdateStatus();
    }

    public float Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }
    
    protected string ExtractionFolder { get; }

    protected string PackageType { get; }

    public event EventHandler<Task<bool>>? Installing; 
    
    public event EventHandler? Installed;

    public event EventHandler? Removed;

    public Package Package
    {
        get => _package;
        set
        {
            SetProperty(ref _package, value);
            UpdateStatus();
        }
    }

    public async Task<bool> UpdateAsync(PackageVersion version)
    {
        if(!await RemoveAsync()) return false;
        return await DownloadAsync(version);
    }

    protected virtual PackageTarget? SelectTarget(PackageVersion version)
    {
        var currentTarget = PlatformHelper.Platform.ToString().ToLower();
        var target = version.Targets?.FirstOrDefault(x => x.Target?.Replace("-", "") == currentTarget);
        return target;
    }
    
    public Task<bool> DownloadAsync(PackageVersion version)
    {
        var task = PerformDownloadAsync(version);

        Installing?.Invoke(this, task);
        
        return task;
    }

    private async Task<bool> PerformDownloadAsync(PackageVersion version)
    {
        try
        {
            Status = PackageStatus.Installing;

            var target = SelectTarget(version);

            if (target is {Url: not null})
            {
                var state = _applicationStateService.AddState($"Downloading {Package.Id}...", AppState.Loading);

                var progress = new Progress<float>(x =>
                {
                    Progress = x;
                    state.StatusMessage = $"Downloading {Package.Id} {(int)(x*100)}%";
                });
                
                //Download
                var result = await _httpService.DownloadAndExtractArchiveAsync(target.Url, ExtractionFolder, progress);

                _applicationStateService.RemoveState(state);

                if (!result)
                {
                    Status = PackageStatus.Available;
                    _applicationStateService.RemoveState(state);
                    return false;
                }
                
                PlatformHelper.ChmodFolder(ExtractionFolder);
                
                Install(target);
                
                InstalledVersion = version;
                
                Installed?.Invoke(this, EventArgs.Empty);

                await Task.Delay(100);
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
            return false;
        }
        return true;
    }
    
    /// <summary>
    /// Gets called after downloading and extracting
    /// Make sure to set Status after completing
    /// </summary>
    protected abstract void Install(PackageTarget target);

    /// <summary>
    /// Can be used to stop processes to avoid removal issues
    /// </summary>
    protected virtual Task PrepareRemoveAsync(PackageTarget target)
    {
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Gets called after deleting the package
    /// Make sure to set Status after completing
    /// </summary>
    protected abstract void Uninstall();

    public async Task<bool> RemoveAsync()
    {
        if (Package.Id == null) throw new NullReferenceException(nameof(Package.Id));

        var currentTarget = PlatformHelper.Platform.ToString().ToLower();

        var target = SelectTarget(_installedVersion!);
        
        if(target != null) await PrepareRemoveAsync(target);
        
        if (Directory.Exists(ExtractionFolder))
        {
            try
            {
                Directory.Delete(ExtractionFolder, true);
            }
            catch (Exception e)
            {
                _logger.Error(e.Message, e);
                return false;
            }
        }
        
        InstalledVersion = null;
        
        Uninstall();
        
        Removed?.Invoke(this, EventArgs.Empty);

        return true;
    }

    private void UpdateStatus()
    {
        var lV = Version.TryParse(Package.Versions?.LastOrDefault()?.Version, out var lastVersion);
        var iV = Version.TryParse(InstalledVersion?.Version ?? "", out var installedVersion);
        
        if (lV && iV && lastVersion > installedVersion)
        {
            Status = PackageStatus.UpdateAvailable;
        }
        else if (iV)
        {
            Status = PackageStatus.Installed;
        }
        else if (!iV && lV)
        {
            Status = PackageStatus.Available;
        }
        else
        {
            Status = PackageStatus.Unavailable;
        }
    }
}