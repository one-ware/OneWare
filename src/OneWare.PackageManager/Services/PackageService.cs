using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Models;

namespace OneWare.PackageManager.Services;

public class PackageService : ObservableObject, IPackageService
{
    private readonly IPackageCatalog _catalog;
    private readonly IPackageDownloader _downloader;
    private readonly IHttpService _httpService;
    private readonly ILogger _logger;
    private readonly ISettingsService _settingsService;
    private readonly IApplicationStateService _applicationStateService;
    private readonly IPackageStateStore _stateStore;
    private readonly IReadOnlyDictionary<string, IPackageInstaller> _installersByType;
    private readonly IPaths _paths;
    private readonly Dictionary<string, Task<PackageInstallResult>> _activeInstalls = new();
    private readonly List<string> _repositoryUrls = [];

    private Task<bool>? _currentRefreshTask;

    public PackageService(IPackageCatalog catalog, IPackageDownloader downloader, IPackageStateStore stateStore,
        IEnumerable<IPackageInstaller> installers, ISettingsService settingsService, ILogger logger,
        IApplicationStateService applicationStateService, IHttpService httpService, IPaths paths)
    {
        _catalog = catalog;
        _downloader = downloader;
        _stateStore = stateStore;
        _settingsService = settingsService;
        _logger = logger;
        _applicationStateService = applicationStateService;
        _httpService = httpService;
        _paths = paths;
        _installersByType = installers.ToDictionary(x => x.PackageType, x => x);
    }

    public bool IsUpdating
    {
        get;
        private set => SetProperty(ref field, value);
    }

    private readonly Dictionary<string, PackageState> _packages = new();

    public IReadOnlyDictionary<string, IPackageState> Packages =>
        _packages.ToDictionary(
            kvp => kvp.Key,
            kvp => (IPackageState)kvp.Value);

    public event EventHandler? PackagesUpdated;
    public event EventHandler<PackageProgressEventArgs>? PackageProgress;

    public void RegisterPackage(Package package)
    {
        _catalog.RegisterStandalone(package);
        if (package.Id == null) return;

        if (_packages.TryGetValue(package.Id, out var state))
        {
            state.Package = package;
            state.InstalledVersion =
                package.Versions?.FirstOrDefault(x => x.Version == state.InstalledVersion?.Version);
            UpdateStatus(state);
        }
        else
        {
            _packages[package.Id] = new PackageState(package);
            UpdateStatus(_packages[package.Id]);
        }

        PackagesUpdated?.Invoke(this, EventArgs.Empty);
    }

    public void RegisterPackageRepository(string url)
    {
        _repositoryUrls.Add(url);
    }

    public async Task<bool> RefreshAsync()
    {
        try
        {
            if (_currentRefreshTask is { IsCompleted: false })
            {
                return await _currentRefreshTask;
            }

            _currentRefreshTask = RefreshInternalAsync();
            return await _currentRefreshTask;
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return false;
        }
    }

    public async Task<PackageInstallResult> InstallAsync(Package package, PackageVersion? version = null,
        bool includePrerelease = false, bool ignoreCompatibility = false)
    {
        if (package.Id == null)
            return new PackageInstallResult { Status = PackageInstallResultReason.NotFound };

        if (!_packages.ContainsKey(package.Id)) RegisterPackage(package);

        return await InstallAsync(package.Id, version, includePrerelease, ignoreCompatibility);
    }

    public async Task<PackageInstallResult> InstallAsync(string packageId, PackageVersion? version = null,
        bool includePrerelease = false, bool ignoreCompatibility = false)
    {
        if (!_packages.TryGetValue(packageId, out var state))
            return new PackageInstallResult { Status = PackageInstallResultReason.NotFound };

        if (_activeInstalls.TryGetValue(packageId, out var active))
            return await active;

        var installTask = InstallInternalAsync(state, version, includePrerelease, ignoreCompatibility);
        _activeInstalls[packageId] = installTask;

        var result = await installTask;
        _activeInstalls.Remove(packageId);
        return result;
    }

    public async Task<PackageInstallResult> UpdateAsync(string packageId, PackageVersion? version = null,
        bool includePrerelease = false, bool ignoreCompatibility = false)
    {
        if (!_packages.TryGetValue(packageId, out var state))
            return new PackageInstallResult { Status = PackageInstallResultReason.NotFound };

        if (_activeInstalls.TryGetValue(packageId, out var active))
            return await active;

        var updateTask = UpdateInternalAsync(state, version, includePrerelease, ignoreCompatibility);
        _activeInstalls[packageId] = updateTask;

        var result = await updateTask;
        _activeInstalls.Remove(packageId);
        return result;
    }

    public async Task<bool> RemoveAsync(string packageId)
    {
        if (!_packages.TryGetValue(packageId, out var state)) return false;

        if (state.InstalledVersion == null) return true;

        var installer = ResolveInstaller(state.Package);
        if (installer == null) return false;

        var version = state.InstalledVersion;
        var target = installer.SelectTarget(state.Package, version);
        if (target == null) return false;

        var context = new PackageInstallContext(state.Package, version, target, GetExtractionPath(state.Package),
            new Progress<float>(_ => { }));

        try
        {
            await installer.PrepareRemoveAsync(context);
            if (Directory.Exists(context.ExtractionPath))
                Directory.Delete(context.ExtractionPath, true);

            var result = await installer.RemoveAsync(context);
            state.InstalledVersion = null;
            state.InstalledVersionWarningText = null;
            state.Status = result.Status;
            state.Progress = 0;
            state.IsIndeterminate = false;

            await SaveInstalledPackagesAsync();

            return true;
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            UpdateStatus(state);
            return false;
        }
    }

    public Task<CompatibilityReport> CheckCompatibilityAsync(string packageId, PackageVersion version)
    {
        if (!_packages.TryGetValue(packageId, out var state))
            return Task.FromResult(new CompatibilityReport(false));

        var installer = ResolveInstaller(state.Package);
        if (installer == null)
            return Task.FromResult(new CompatibilityReport(false));

        return installer.CheckCompatibilityAsync(state.Package, version);
    }

    public async Task<string?> DownloadLicenseAsync(Package package)
    {
        var url = package.Tabs?.FirstOrDefault(x => x.Title == "License")?.ContentUrl;
        if (url == null) return null;

        return await _httpService.DownloadTextAsync(url);
    }

    public async Task<IImage?> DownloadPackageIconAsync(Package package)
    {
        return package.IconUrl != null
            ? await _httpService.DownloadImageAsync(package.IconUrl)
            : null;
    }

    private async Task<bool> RefreshInternalAsync()
    {
        await WaitForInstallsAsync();

        IsUpdating = true;
        var result = true;

        try
        {
            var customRepositories =
                _settingsService.GetSettingValue<ObservableCollection<string>>("PackageManager_Sources");

            var allRepos = _repositoryUrls.Concat(customRepositories);
            result = await _catalog.RefreshAsync(allRepos);

            var installed = await _stateStore.LoadAsync();

            var nextStates = new Dictionary<string, PackageState>();

            foreach (var (id, manifest) in _catalog.Manifests)
                nextStates[id] = new PackageState(manifest);

            foreach (var installedPackage in installed.Values)
            {
                if (!nextStates.TryGetValue(installedPackage.Id, out var state))
                {
                    var stub = new Package
                    {
                        Id = installedPackage.Id,
                        Type = installedPackage.Type,
                        Name = installedPackage.Name,
                        Category = installedPackage.Category,
                        Versions =
                        [
                            new PackageVersion
                            {
                                Version = installedPackage.InstalledVersion
                            }
                        ],
                        Description = installedPackage.Description,
                        License = installedPackage.License
                    };
                    state = new PackageState(stub);
                    nextStates[installedPackage.Id] = state;
                }

                state.InstalledVersion =
                    state.Package.Versions?.FirstOrDefault(x => x.Version == installedPackage.InstalledVersion);
            }

            _packages.Clear();
            foreach (var (id, state) in nextStates)
            {
                UpdateStatus(state);
                _packages[id] = state;
            }

            PackagesUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            result = false;
        }
        finally
        {
            IsUpdating = false;
        }

        return result;
    }

    private async Task WaitForInstallsAsync()
    {
        var activeInstallTasks = _activeInstalls.Select(x => x.Value).ToArray();
        if (activeInstallTasks.Length != 0) await Task.WhenAll(activeInstallTasks);
    }

    private async Task<PackageInstallResult> InstallInternalAsync(PackageState state, PackageVersion? version,
        bool includePrerelease, bool ignoreCompatibility)
    {
        var selectedVersion = ResolveVersion(state, version, includePrerelease);
        if (selectedVersion == null)
            return new PackageInstallResult { Status = PackageInstallResultReason.NotFound };

        if (state.Status is PackageStatus.Installed or PackageStatus.NeedRestart &&
            state.InstalledVersion?.Version == selectedVersion.Version)
            return new PackageInstallResult { Status = PackageInstallResultReason.AlreadyInstalled };

        var installer = ResolveInstaller(state.Package);
        if (installer == null)
            return new PackageInstallResult { Status = PackageInstallResultReason.NotFound };

        var compatibility = await installer.CheckCompatibilityAsync(state.Package, selectedVersion);
        if (!compatibility.IsCompatible && !ignoreCompatibility)
        {
            _logger.Error(compatibility.Report ?? "Package is incompatible.");
            return new PackageInstallResult
            {
                Status = PackageInstallResultReason.Incompatible,
                CompatibilityRecord = compatibility
            };
        }

        var target = installer.SelectTarget(state.Package, selectedVersion);
        if (target == null)
        {
            _logger.Warning(
                $"No compatible target found for package {state.Package.Id} version {selectedVersion.Version}");
            
            return new PackageInstallResult { Status = PackageInstallResultReason.NotFound };
        }
        
        return await DownloadAndInstallAsync(state, selectedVersion, target, installer, compatibility);
    }

    private async Task<PackageInstallResult> UpdateInternalAsync(PackageState state, PackageVersion? version,
        bool includePrerelease, bool ignoreCompatibility)
    {
        var selectedVersion = ResolveVersion(state, version, includePrerelease);
        if (selectedVersion == null)
            return new PackageInstallResult { Status = PackageInstallResultReason.NotFound };

        if (state.InstalledVersion == null)
            return await InstallInternalAsync(state, selectedVersion, includePrerelease, ignoreCompatibility);

        var removed = await RemoveAsync(state.Package.Id!);
        if (!removed)
            return new PackageInstallResult { Status = PackageInstallResultReason.ErrorDownloading };

        return await InstallInternalAsync(state, selectedVersion, includePrerelease, ignoreCompatibility);
    }

    private async Task<PackageInstallResult> DownloadAndInstallAsync(PackageState state, PackageVersion version,
        PackageTarget target, IPackageInstaller installer, CompatibilityReport compatibility)
    {
        try
        {
            state.Status = PackageStatus.Installing;
            var stateHandle = _applicationStateService.AddState($"Downloading {state.Package.Id}...", AppState.Loading);

            var progress = new Progress<float>(value =>
            {
                state.Progress = value;
                if (value < 1)
                {
                    stateHandle.StatusMessage = $"Downloading {state.Package.Id} {(int)(value * 100)}%";
                    state.IsIndeterminate = false;
                }
                else
                {
                    stateHandle.StatusMessage = $"Extracting {state.Package.Id}...";
                    state.IsIndeterminate = true;
                }

                PackageProgress?.Invoke(this,
                    new PackageProgressEventArgs(state.Package.Id!, state.Status, state.Progress,
                        state.IsIndeterminate));
            });

            var extractionPath = GetExtractionPath(state.Package);
            var url = target.Url ??
                      $"{state.Package.SourceUrl}/{version.Version}/{state.Package.Id}_{version.Version}_{target.Target}.zip";

            var success = await _downloader.DownloadAndExtractAsync(url, extractionPath, progress);
            _applicationStateService.RemoveState(stateHandle);
            state.IsIndeterminate = false;

            if (!success)
            {
                UpdateStatus(state);
                return new PackageInstallResult { Status = PackageInstallResultReason.ErrorDownloading };
            }

            PlatformHelper.ChmodFolder(extractionPath);

            var result = await installer.InstallAsync(new PackageInstallContext(state.Package, version, target,
                extractionPath, progress));

            state.InstalledVersion = version;
            state.InstalledVersionWarningText = result.InstalledVersionWarningText;
            state.Status = result.Status;
            state.Progress = 0;

            await SaveInstalledPackagesAsync();

            return new PackageInstallResult
            {
                Status = result.Status is PackageStatus.Installed or PackageStatus.NeedRestart
                    ? PackageInstallResultReason.Installed
                    : PackageInstallResultReason.ErrorDownloading,
                CompatibilityRecord = compatibility
            };
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            UpdateStatus(state);
            return new PackageInstallResult { Status = PackageInstallResultReason.ErrorDownloading };
        }
    }

    private void UpdateStatus(PackageState state)
    {
        if (state.Status == PackageStatus.NeedRestart) return;

        var lastPrerelease = state.Package.Versions?.Where(x => x.IsPrerelease).LastOrDefault();
        var lastStable = state.Package.Versions?.Where(x => !x.IsPrerelease).LastOrDefault();

        var hasStable = Version.TryParse(lastStable?.Version, out var lastVersion);
        var hasPrerelease = Version.TryParse(lastPrerelease?.Version, out var lastPrereleaseVersion);
        var hasInstalled = Version.TryParse(state.InstalledVersion?.Version ?? "", out var installedVersion);

        if (hasStable && hasInstalled && lastVersion > installedVersion)
            state.Status = PackageStatus.UpdateAvailable;
        else if (hasInstalled && hasPrerelease && lastPrereleaseVersion > installedVersion)
            state.Status = PackageStatus.UpdateAvailablePrerelease;
        else if (hasInstalled)
            state.Status = PackageStatus.Installed;
        else if (!hasInstalled && hasStable)
            state.Status = PackageStatus.Available;
        else
            state.Status = PackageStatus.Unavailable;
    }

    private PackageVersion? ResolveVersion(PackageState state, PackageVersion? version, bool includePrerelease)
    {
        if (version != null) return version;

        return state.Package.Versions?.LastOrDefault(x => includePrerelease || !x.IsPrerelease);
    }

    private IPackageInstaller? ResolveInstaller(Package package)
    {
        if (package.Type == null) return null;
        return _installersByType.GetValueOrDefault(package.Type);
    }

    private string GetExtractionPath(Package package)
    {
        if (package.Id == null) throw new InvalidOperationException("Package Id is required.");

        return package.Type switch
        {
            "Plugin" => Path.Combine(_paths.PluginsDirectory, package.Id),
            "NativeTool" => Path.Combine(_paths.NativeToolsDirectory, package.Id),
            "Hardware" => Path.Combine(_paths.PackagesDirectory, "Hardware",
                package.Id),
            "Library" => Path.Combine(_paths.PackagesDirectory, "Libraries",
                package.Id),
            _ => Path.Combine(_paths.PackagesDirectory, package.Id)
        };
    }

    private async Task SaveInstalledPackagesAsync()
    {
        var installedPackages = _packages.Values
            .Where(x => x.InstalledVersion is not null)
            .Select(x => new InstalledPackage(x.Package.Id!, x.Package.Type!, x.Package.Name!,
                x.Package.Category, x.Package.Description, x.Package.License,
                x.InstalledVersion!.Version!))
            .ToArray();

        await _stateStore.SaveAsync(installedPackages);
    }
}