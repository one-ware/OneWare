using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.Models;

public abstract class PackageModel : ObservableObject
{
    private readonly IApplicationStateService _applicationStateService;
    private readonly ILogger _logger;

    protected readonly IHttpService HttpService;
    private PackageVersion? _installedVersion;
    private Package _package;

    protected PackageModel(Package package,
        string packageType,
        string extractionFolder,
        IHttpService httpService,
        ILogger logger,
        IApplicationStateService applicationStateService)
    {
        _package = package;
        HttpService = httpService;
        _logger = logger;
        _applicationStateService = applicationStateService;
        ExtractionFolder = extractionFolder;
        PackageType = packageType;

        UpdateStatus();
    }

    public PackageVersion? InstalledVersion
    {
        get => _installedVersion;
        set
        {
            SetProperty(ref _installedVersion, value);
            UpdateStatus();
        }
    }

    public string? InstalledVersionWarningText
    {
        get;
        set => SetProperty(ref field, value);
    }

    public PackageStatus Status
    {
        get;
        protected set => SetProperty(ref field, value);
    }

    public bool IsIndeterminate
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public float Progress
    {
        get;
        private set => SetProperty(ref field, value);
    }

    protected string ExtractionFolder { get; }

    protected string PackageType { get; }

    public Package Package
    {
        get => _package;
        set
        {
            SetProperty(ref _package, value);
            UpdateStatus();
        }
    }

    public event EventHandler<Task<bool>>? Installing;

    public event EventHandler? Installed;

    public event EventHandler? Removed;

    public async Task<bool> UpdateAsync(PackageVersion version, bool ignoreCompatibility = false)
    {
        var compat = await CheckCompatibilityAsync(version);

        if (!compat.IsCompatible && !ignoreCompatibility)
        {
            _logger.Error(compat.Report!);
            return false;
        }

        if (!await RemoveAsync()) return false;
        return await DownloadAsync(version);
    }

    protected virtual PackageTarget? SelectTarget(PackageVersion version)
    {
        var currentTarget = PlatformHelper.Platform.ToString().ToLower();
        var target = version.Targets?.FirstOrDefault(x => x.Target?.Replace("-", "") == currentTarget) ??
                     version.Targets?.FirstOrDefault(x => x.Target == "all");
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

            if (target is not null)
            {
                var zipUrl = target.Url ??
                             $"{Package.SourceUrl}/{version.Version}/{Package.Id}_{version.Version}_{target.Target}.zip";

                var state = _applicationStateService.AddState($"Downloading {Package.Id}...", AppState.Loading);

                var progress = new Progress<float>(x =>
                {
                    Progress = x;
                    if (x < 1)
                    {
                        state.StatusMessage = $"Downloading {Package.Id} {(int)(x * 100)}%";
                    }
                    else
                    {
                        state.StatusMessage = $"Extracting {Package.Id}...";
                        IsIndeterminate = true;
                    }
                });

                //Download
                var result = await HttpService.DownloadAndExtractArchiveAsync(zipUrl, ExtractionFolder, progress);

                _applicationStateService.RemoveState(state);

                IsIndeterminate = false;

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
    ///     Gets called after downloading and extracting
    ///     Make sure to set Status after completing
    /// </summary>
    protected abstract void Install(PackageTarget target);

    /// <summary>
    ///     Can be used to stop processes to avoid removal issues
    /// </summary>
    protected virtual Task PrepareRemoveAsync(PackageTarget target)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Gets called after deleting the package
    ///     Make sure to set Status after completing
    /// </summary>
    protected abstract void Uninstall();

    public async Task<bool> RemoveAsync()
    {
        if (Package.Id == null) throw new NullReferenceException(nameof(Package.Id));

        var currentTarget = PlatformHelper.Platform.ToString().ToLower();

        var target = SelectTarget(_installedVersion!);

        if (target != null) await PrepareRemoveAsync(target);

        if (Directory.Exists(ExtractionFolder))
            try
            {
                Directory.Delete(ExtractionFolder, true);
            }
            catch (Exception e)
            {
                _logger.Error(e.Message, e);
                return false;
            }

        InstalledVersion = null;

        Uninstall();

        Removed?.Invoke(this, EventArgs.Empty);

        return true;
    }

    private void UpdateStatus()
    {
        if (Status == PackageStatus.NeedRestart) return;

        var lastPrerelease = Package.Versions?.Where(x => x.IsPrerelease).LastOrDefault();
        var lastStable = Package.Versions?.Where(x => !x.IsPrerelease).LastOrDefault();

        var lV = Version.TryParse(lastStable?.Version, out var lastVersion);
        var lpV = Version.TryParse(lastPrerelease?.Version, out var lastPrereleaseVersion);
        var iV = Version.TryParse(InstalledVersion?.Version ?? "", out var installedVersion);

        if (lV && iV && lastVersion > installedVersion)
            Status = PackageStatus.UpdateAvailable;
        else if (iV && lpV && lastPrereleaseVersion > installedVersion)
            Status = PackageStatus.UpdateAvailablePrerelease;
        else if (iV)
            Status = PackageStatus.Installed;
        else if (!iV && lV)
            Status = PackageStatus.Available;
        else
            Status = PackageStatus.Unavailable;
    }

    public virtual Task<CompatibilityReport> CheckCompatibilityAsync(PackageVersion version)
    {
        return Task.FromResult(new CompatibilityReport(true));
    }
}