using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Enums;
using OneWare.PackageManager.Serializer;

namespace OneWare.PackageManager.Models;

public abstract class PackageModel(
    Package package,
    string packageType,
    string extractionFolder,
    IHttpService httpService,
    ILogger logger)
    : ObservableObject
{
    private PackageVersion? _installedVersion;
    public PackageVersion? InstalledVersion
    {
        get => _installedVersion;
        set => SetProperty(ref _installedVersion, value);
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
        set => SetProperty(ref _status, value);
    }
    
    private float _progress;
    public float Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }
    
    protected string ExtractionFolder { get; } = extractionFolder;

    protected string PackageType { get; } = packageType;

    public event EventHandler? Installed;

    public event EventHandler? Removed;

    public Package Package { get; set; } = package;

    public async Task UpdateAsync(PackageVersion version)
    {
        await RemoveAsync();
        await DownloadAsync(version);
    }
    
    public async Task DownloadAsync(PackageVersion version)
    {
        try
        {
            Status = PackageStatus.Installing;
            
            var currentTarget = PlatformHelper.Platform.ToString().ToLower();

            var selectedVersion = version;
            
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
                if (!await httpService.DownloadAndExtractArchiveAsync(target.Url, ExtractionFolder, progress))
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
            logger.Error(e.Message, e);
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

    public Task RemoveAsync()
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
                logger.Error(e.Message, e);
                return Task.CompletedTask;
            }
        }
        
        InstalledVersion = null;
        
        Uninstall();
        
        Removed?.Invoke(this, EventArgs.Empty);
        
        return Task.CompletedTask;
    }
}