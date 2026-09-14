using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Installers;
using OneWare.PackageManager.Models;

namespace OneWare.PackageManager.Services;

public partial class PackageService : ObservableObject, IPackageService, IPackageOperationService, IPackageDiscoveryService, IDisposable
{
    public IReadOnlyList<string> FeaturedPackageIds => (_catalog as IPackageDiscoveryService)?.FeaturedPackageIds ?? [];
    public void RegisterOfficialSource(string url) => (_catalog as IPackageDiscoveryService)?.RegisterOfficialSource(url);
    private readonly ICompositeServiceProvider _compositeServiceProvider;
    private readonly IPackageCatalog _catalog;
    private readonly IPackageDownloader _downloader;
    private readonly IHttpService _httpService;
    private readonly ILogger _logger;
    private readonly ISettingsService _settingsService;
    private readonly IPackageStateStore _stateStore;
    private readonly IPackageInstaller _defaultInstaller;
    private readonly IPaths _paths;
    private readonly Dictionary<string, Type> _installersByType = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, CancellationTokenSource> _installCancellation = new();
    private readonly List<string[]> _repositoryUrls = [];
    
    private bool _disposed;
    private Task<bool>? _currentRefreshTask;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public PackageService(IPackageCatalog catalog, IPackageDownloader downloader, IPackageStateStore stateStore,
        ISettingsService settingsService, ILogger logger, IApplicationStateService applicationStateService,
        IHttpService httpService, IPaths paths, ICompositeServiceProvider compositeServiceProvider, GenericPackageInstaller genericPackageInstaller)
    {
        _catalog = catalog;
        _downloader = downloader;
        _stateStore = stateStore;
        _settingsService = settingsService;
        _logger = logger;
        _httpService = httpService;
        _paths = paths;
        _defaultInstaller = genericPackageInstaller;
        _compositeServiceProvider = compositeServiceProvider;

        _settingsService.Saved += OnSettingsSaved;
    }

    public bool IsUpdating
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool IsLoaded
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

    public void Dispose()
    {
        if (_disposed) 
            return;
        
        _disposed = true;
        PackagesUpdated = null; 
        PackageProgress = null; 
        
        foreach (var cts in _installCancellation.Values.ToArray())
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // already completed and disposed by the owning install task
            }
        }

        _installCancellation.Clear();
        _currentRefreshTask = null;

        _settingsService.Saved -= OnSettingsSaved;
    }

    public void RegisterPackage(Package package)
    {
        _catalog.RegisterStandalone(package);
        if (package.Id == null) return;

        if (_packages.TryGetValue(package.Id, out var state))
        {
            state.Package = package;
            state.InstalledVersion =
                package.Versions?.FirstOrDefault(x => x.Version == state.InstalledVersion?.Version) ?? state.InstalledVersion;
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
        _repositoryUrls.Add([url]);
    }

    public void RegisterPackageRepositoryWithFallback(string[] urls)
    {
        _repositoryUrls.Add(urls);
    }

    public void RegisterInstaller<T>(string packageType) where T : IPackageInstaller
    {
        if (string.IsNullOrWhiteSpace(packageType))
            throw new ArgumentException("Package type is required.", nameof(packageType));

        _installersByType[packageType] = typeof(T);
    }

    public void CancelInstall(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId)) return;
        if (_installCancellation.TryGetValue(packageId, out var cts))
            try { cts.Cancel(); }
            catch (ObjectDisposedException) { /* The owning operation just completed. */ }
    }

    [Obsolete]
    public Task<bool> RefreshAsync()
    {
        return RefreshAsync(false);
    }

    public async Task<bool> RefreshAsync(bool force)
    {
        try
        {
            // Join an already running refresh unless the caller requires one that uses the current settings
            if (!force && _currentRefreshTask is { IsCompleted: false } pending)
            {
                return await pending;
            }

            // Serialize refreshes, a forced refresh must not run in parallel with an outdated one
            await _refreshLock.WaitAsync();
            try
            {
                await _operationLock.WaitAsync();
                try
                {
                    var refreshTask = RefreshInternalAsync();
                    _currentRefreshTask = refreshTask;
                    return await refreshTask;
                }
                finally { _operationLock.Release(); }
            }
            finally
            {
                _refreshLock.Release();
            }
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return false;
        }
    }

    public Task<PackageInstallResult> InstallAsync(Package package, PackageVersion? version = null,
        bool includePrerelease = false, bool ignoreCompatibility = false)
    {
        return InstallAsync(package, version, includePrerelease, ignoreCompatibility, CancellationToken.None);
    }

    public Task<PackageInstallResult> InstallAsync(string packageId, PackageVersion? version = null,
        bool includePrerelease = false, bool ignoreCompatibility = false)
    {
        return InstallAsync(packageId, version, includePrerelease, ignoreCompatibility, CancellationToken.None);
    }

    public Task<PackageInstallResult> UpdateAsync(string packageId, PackageVersion? version = null,
        bool includePrerelease = false, bool ignoreCompatibility = false)
    {
        return UpdateAsync(packageId, version, includePrerelease, ignoreCompatibility, CancellationToken.None);
    }

    public async Task<PackageInstallResult> InstallAsync(Package package, PackageVersion? version,
        bool includePrerelease, bool ignoreCompatibility, CancellationToken cancellationToken)
    {
        if (package.Id == null)
            return new PackageInstallResult { Status = PackageInstallResultReason.NotFound };

        if (!_packages.ContainsKey(package.Id)) RegisterPackage(package);

        return await InstallAsync(package.Id, version, includePrerelease, ignoreCompatibility, cancellationToken);
    }

    public async Task<PackageInstallResult> InstallAsync(string packageId, PackageVersion? version,
        bool includePrerelease, bool ignoreCompatibility, CancellationToken cancellationToken)
    {
        if (!_packages.ContainsKey(packageId))
            return new PackageInstallResult { Status = PackageInstallResultReason.NotFound };
        return await ExecuteLegacyAsync(packageId, version, includePrerelease, cancellationToken);
    }

    public async Task<PackageInstallResult> UpdateAsync(string packageId, PackageVersion? version,
        bool includePrerelease, bool ignoreCompatibility, CancellationToken cancellationToken)
    {
        return await InstallAsync(packageId, version, includePrerelease, ignoreCompatibility, cancellationToken);
    }

    public async Task<bool> RemoveAsync(string packageId)
    {
        await _operationLock.WaitAsync();
        try
        {
            new PackageDependency { Id = packageId }.Validate();
            if (_packages.TryGetValue(packageId, out var packageState) && packageState.Package.Id != packageId)
                throw new InvalidOperationException("Package identity does not match the installed record.");
            var dependents = InstalledRecords().Values.Where(x => x.Id != packageId &&
                x.Dependencies?.Any(d => d.Id == packageId) == true).Select(x => x.Name).ToArray();
            if (dependents.Length > 0)
            {
                _logger.Warning($"Cannot remove {packageId}; required by {string.Join(", ", dependents)}.");
                return false;
            }
            var removed = await RemoveInternalAsync(packageId);
            if (removed) _installedRecords.Remove(packageId);
            return removed;
        }
        catch (InvalidOperationException ex) { _logger.Warning(ex.Message); return false; }
        finally { _operationLock.Release(); }
    }

    private async Task<bool> RemoveInternalAsync(string packageId)
    {
        if (!_packages.TryGetValue(packageId, out var state)) return false;

        if (state.InstalledVersion == null) return true;

        var installer = ResolveInstaller(state.Package);

        var extractionPath = installer.GetExtractionPath(state.Package, _paths);

        var version = state.InstalledVersion;
        var target = installer.SelectTarget(state.Package, version) ?? new PackageTarget { Target = "all" };

        var context = new PackageInstallContext(state.Package, version, target, extractionPath,
            new Progress<float>(_ => { }));
        var backup = extractionPath + ".backup-" + Guid.NewGuid().ToString("N");
        var previousStatus = state.Status;
        var moved = false;
        try
        {
            await installer.PrepareRemoveAsync(context);
            if (Directory.Exists(context.ExtractionPath))
            { Directory.Move(context.ExtractionPath, backup); moved = true; }

            state.InstalledVersion = null;
            await SaveInstalledPackagesAsync();
            // Removal hooks may partially affect a loaded plugin before throwing.
            if (state.Package.Type == "Plugin") _pluginsPendingRestart.Add(packageId);
            var result = await installer.RemoveAsync(context);
            state.InstalledVersionWarningText = null;
            state.Status = result.Status;
            state.Progress = 0;
            state.IsIndeterminate = false;

            try { if (moved) Directory.Delete(backup, true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { _logger.Warning($"Removal backup retained: {ex.Message}"); }

            // A package that no repository offers anymore only existed as a stub for its installation.
            // Once it is removed there is nothing left to show or install, so it is dropped entirely.
            if (!_catalog.Manifests.ContainsKey(packageId) && _packages.Remove(packageId))
                PackagesUpdated?.Invoke(this, EventArgs.Empty);

            return true;
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            state.InstalledVersion = version;
            state.Status = _pluginsPendingRestart.Contains(packageId) ? PackageStatus.NeedRestart : previousStatus;
            try
            {
                if (moved && Directory.Exists(backup) && !Directory.Exists(extractionPath)) Directory.Move(backup, extractionPath);
            }
            catch (Exception rollbackError)
            { _logger.Error($"Removal rollback failed; repair the installation from {backup}: {rollbackError.Message}", rollbackError); }
            try { await SaveInstalledPackagesAsync(); }
            catch (Exception writeError) { _logger.Error(writeError.Message, writeError); }
            return false;
        }
    }

    public Task<CompatibilityReport> CheckCompatibilityAsync(string packageId, PackageVersion version)
    {
        if (!_packages.TryGetValue(packageId, out var state))
            return Task.FromResult(new CompatibilityReport(false));

        var installer = ResolveInstaller(state.Package);

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
        if (!string.IsNullOrWhiteSpace(package.Icon))
            return CreateImageFromBase64(package.Icon, package.Id);

        if (package.IconUrl == null) return null;

        return await _httpService.DownloadImageAsync(package.IconUrl, true);
    }

    private IImage? CreateImageFromBase64(string icon, string? packageId)
    {
        try
        {
            var base64 = icon.Trim();
            var mediaType = string.Empty;

            if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var separatorIndex = base64.IndexOf(',');
                if (separatorIndex < 0)
                {
                    _logger.Warning($"Invalid embedded icon data URI for package {packageId}.");
                    return null;
                }

                mediaType = base64[..separatorIndex];
                base64 = base64[(separatorIndex + 1)..];
            }

            var bytes = Convert.FromBase64String(base64);
            using var stream = new MemoryStream(bytes);

            if (IsSvgIcon(bytes, mediaType))
            {
                var svg = SvgSource.LoadFromStream(stream);
                return svg is null
                    ? null
                    : new SvgImage
                    {
                        Source = svg
                    };
            }

            return new Bitmap(stream);
        }
        catch (FormatException e)
        {
            _logger.Warning($"Invalid embedded icon base64 for package {packageId}: {e.Message}");
        }
        catch (ArgumentException e)
        {
            _logger.Warning($"Invalid embedded icon for package {packageId}: {e.Message}");
        }
        catch (NotSupportedException e)
        {
            _logger.Warning($"Unsupported embedded icon for package {packageId}: {e.Message}");
        }

        return null;
    }

    private static bool IsSvgIcon(byte[] bytes, string mediaType)
    {
        if (mediaType.Contains("svg", StringComparison.OrdinalIgnoreCase)) return true;

        var prefix = Encoding.UTF8.GetString(bytes.AsSpan(0, Math.Min(bytes.Length, 512)));
        return prefix.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> RefreshInternalAsync()
    {
        IsUpdating = true;
        var result = true;

        try
        {
            var customRepositories = _settingsService
                .GetSettingValue<ObservableCollection<string>>("PackageManager_Sources")
                .Select(item => new[] { item })
                .ToList();

            var onlyCustomSources = _settingsService.GetSettingValue<bool>("PackageManager_OnlyCustomSources");
            var allRepos = onlyCustomSources
                ? customRepositories
                : _repositoryUrls.Concat(customRepositories).ToList();

            result = await _catalog.RefreshAsync(allRepos);

            var installed = await _stateStore.LoadAsync();
            _installedRecords.Clear();
            foreach (var (id, record) in installed) _installedRecords[id] = record;

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
                                Version = installedPackage.InstalledVersion,
                                Dependencies = installedPackage.Dependencies
                            }
                        ],
                        Description = installedPackage.Description,
                        License = installedPackage.License
                    };
                    state = new PackageState(stub);
                    nextStates[installedPackage.Id] = state;
                }

                var installedVersion =
                    state.Package.Versions?.FirstOrDefault(x => x.Version == installedPackage.InstalledVersion);

                if (installedVersion == null)
                {
                    var newVersion = new PackageVersion { Version = installedPackage.InstalledVersion,
                        Dependencies = installedPackage.Dependencies };

                    state.Package.Versions =
                        new[] { newVersion }.Concat(state.Package.Versions ?? Enumerable.Empty<PackageVersion>())
                            .ToArray();

                    installedVersion = newVersion;
                }

                state.InstalledVersion = installedVersion;
            }

            foreach (var (id, previous) in _packages)
                if (previous.Status == PackageStatus.NeedRestart && nextStates.TryGetValue(id, out var next) &&
                    previous.InstalledVersion?.Version == next.InstalledVersion?.Version)
                    next.Status = PackageStatus.NeedRestart;

            _packages.Clear();
            foreach (var (id, state) in nextStates)
            {
                UpdateStatus(state);
                _packages[id] = state;
            }

            PackagesUpdated?.Invoke(this, EventArgs.Empty);
            IsLoaded = true;
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

    private void OnSettingsSaved(object? sender, EventArgs e)
    {
        _ = RefreshAsync(false);
    }

    private void UpdateStatus(PackageState state)
    {
        if (state.Status == PackageStatus.NeedRestart) return;

        var target = state.ResolveTargetVersion();

        var hasTarget = SemanticVersion.TryParse(target?.Version, out var targetVersion);
        var hasInstalled = SemanticVersion.TryParse(state.InstalledVersion?.Version, out var installedVersion);

        // An installed package stays removable even when its version string cannot be parsed or the
        // package disappeared from every repository, otherwise it can never be uninstalled again.
        if (!hasInstalled && state.InstalledVersion != null)
        {
            state.Status = PackageStatus.Installed;
            return;
        }

        if (hasInstalled && hasTarget && targetVersion > installedVersion)
            state.Status = target!.IsPrerelease
                ? PackageStatus.UpdateAvailablePrerelease
                : PackageStatus.UpdateAvailable;
        else if (hasInstalled)
            state.Status = PackageStatus.Installed;
        else if (hasTarget)
            state.Status = PackageStatus.Available;
        else
            state.Status = PackageStatus.Unavailable;
    }

    private IPackageInstaller ResolveInstaller(Package package)
    {
        if (string.IsNullOrWhiteSpace(package.Type)) return _defaultInstaller;

        var installerType = _installersByType.GetValueOrDefault(package.Type);

        if (installerType == null) return _defaultInstaller;
        
        return (_compositeServiceProvider.Resolve(installerType) as IPackageInstaller) ?? _defaultInstaller;
    }

    private async Task SaveInstalledPackagesAsync()
    {
        await _stateStore.SaveAsync(InstalledRecords().Values);
    }
}
