﻿using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Models;

namespace OneWare.PackageManager.Services;

public partial class PackageService : ObservableObject, IPackageService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private bool _isUpdating;

    private readonly Dictionary<Package, Task<bool>> _activeInstalls = new();
    private readonly IHttpService _httpService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;
    private readonly string _packageDataBasePath;
    private readonly Lock _installLock = new();
    private CancellationTokenSource? _cancellationTokenSource;

    private readonly Func<Package, PluginPackageModel> _pluginFactory;
    private readonly Func<Package, NativeToolPackageModel> _nativeToolFactory;
    private readonly Func<Package, HardwarePackageModel> _hardwareFactory;
    private readonly Func<Package, LibraryPackageModel> _libraryFactory;

    public PackageService(
        IHttpService httpService,
        ISettingsService settingsService,
        ILogger logger,
        IPaths paths,
        Func<Package, PluginPackageModel> pluginFactory,
        Func<Package, NativeToolPackageModel> nativeToolFactory,
        Func<Package, HardwarePackageModel> hardwareFactory,
        Func<Package, LibraryPackageModel> libraryFactory)
    {
        _httpService = httpService;
        _settingsService = settingsService;
        _logger = logger;
        _pluginFactory = pluginFactory;
        _nativeToolFactory = nativeToolFactory;
        _hardwareFactory = hardwareFactory;
        _libraryFactory = libraryFactory;

        _packageDataBasePath = Path.Combine(paths.PackagesDirectory,
            $"{paths.AppName.ToLower().Replace(" ", "")}-packages.json");

        LoadInstalledPackagesDatabase();
    }

    public bool IsUpdating
    {
        get => _isUpdating;
        private set => SetProperty(ref _isUpdating, value);
    }

    public event EventHandler? PackagesUpdated;

    public List<string> PackageRepositories { get; } = [];
    private List<Package> StandalonePackages { get; } = [];
    public Dictionary<string, PackageModel> Packages { get; } = [];

    public void RegisterPackageRepository(string url)
    {
        PackageRepositories.Add(url);
    }

    public PackageModel? GetPackageModel(Package package)
    {
        return Packages.GetValueOrDefault(package.Id!);
    }

    public void RegisterPackage(Package package)
    {
        StandalonePackages.Add(package);

        if (Packages.TryGetValue(package.Id!, out var pkg))
        {
            pkg.Package = package;
            pkg.InstalledVersion = package.Versions?.FirstOrDefault(x => x.Version == pkg.InstalledVersion?.Version);
        }
        else
        {
            AddPackage(package);
        }
    }

    public async Task<bool> LoadPackagesAsync()
    {
        if (_cancellationTokenSource is not null) await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource = new CancellationTokenSource();

        await WaitForInstallsAsync();

        IsUpdating = true;

        var result = true;

        var customRepositories =
            _settingsService.GetSettingValue<ObservableCollection<string>>("PackageManager_Sources");

        var allRepos = PackageRepositories.Concat(customRepositories);
        var newPackages = new List<Package>();

        foreach (var repository in allRepos)
        {
            var loadedPackages = await LoadPackageRepositoryAsync(repository, _cancellationTokenSource.Token);

            if (_cancellationTokenSource.IsCancellationRequested) break;

            if (loadedPackages != null)
                foreach (var package in loadedPackages)
                {
                    newPackages.Add(package);

                    if (package.Id != null && Packages.TryGetValue(package.Id, out var pkg))
                    {
                        pkg.Package = package;
                    }
                    else
                    {
                        AddPackage(package);
                    }
                }

            result = result && loadedPackages != null;
        }

        foreach (var removedPackage in Packages.Select(x => x.Value.Package)
                     .Where(x => !newPackages.Contains(x) && !StandalonePackages.Contains(x))
                     .ToArray())
        {
            Packages.Remove(removedPackage.Id!);
        }

        PackagesUpdated?.Invoke(this, EventArgs.Empty);

        IsUpdating = false;

        return result;
    }

    public Task<bool> InstallAsync(Package package)
    {
        lock (_installLock)
        {
            if (Packages.TryGetValue(package.Id!, out var model))
            {
                if (!(model.Package.Versions?.Any() ?? false)) return Task.FromResult(false);

                switch (model.Status)
                {
                    case PackageStatus.Available or PackageStatus.UpdateAvailable:
                        _logger.Log($"Downloading {model.Package.Name}...", ConsoleColor.DarkCyan, true, Brushes.DarkCyan);
                        return model.DownloadAsync(model.Package.Versions.Last());
                    case PackageStatus.Installing:
                        if (_activeInstalls.TryGetValue(model.Package, out var task)) return task;
                        return Task.FromResult(model.Status is PackageStatus.Installed or PackageStatus.UpdateAvailable);
                    case PackageStatus.Installed:
                        return Task.FromResult(true);
                }
            }
        }

        return Task.FromResult(false);
    }

    private void AddPackage(Package package, string? installedVersion = null)
    {
        if (package.Id == null) throw new Exception("Package ID cannot be empty");
        if (Packages.ContainsKey(package.Id))
        {
            Console.WriteLine(package.Id + " is already installed.");
            return;
        }

        PackageModel model = package.Type switch
        {
            "Plugin" => _pluginFactory(package),
            "NativeTool" => _nativeToolFactory(package),
            "Hardware" => _hardwareFactory(package),
            "Library" => _libraryFactory(package),
            _ => throw new Exception($"Package Type invalid/missing for {package.Name}!")
        };

        model.InstalledVersion = package.Versions?.FirstOrDefault(x => x.Version == installedVersion);
        Packages.Add(package.Id, model);

        Observable.FromEventPattern<Task<bool>>(model, nameof(model.Installing))
            .Subscribe(x => ObserveInstall((x.Sender as PackageModel)!.Package, x.EventArgs));

        Observable.FromEventPattern(model, nameof(model.Installed))
            .Subscribe(x => _ = SaveInstalledPackagesDatabaseAsync());

        Observable.FromEventPattern(model, nameof(model.Removed))
            .Subscribe(x => _ = SaveInstalledPackagesDatabaseAsync());
    }

    private async Task WaitForInstallsAsync()
    {
        Task<bool>[] activeInstallTasks;
        lock (_installLock)
        {
            activeInstallTasks = _activeInstalls.Select(x => x.Value).ToArray();
        }

        if (activeInstallTasks.Length != 0)
            await Task.WhenAll(activeInstallTasks.ToArray());
    }

    private async Task<Package[]?> LoadPackageRepositoryAsync(string url, CancellationToken cancellationToken)
    {
        var repositoryString = await _httpService.DownloadTextAsync(url, TimeSpan.FromSeconds(10), cancellationToken);
        if (repositoryString == null) return null;

        var trimmed = MyRegex().Replace(repositoryString, "");

        var packages = new List<Package>();

        if (trimmed.StartsWith("{\"packages\":"))
        {
            try
            {
                var repository = JsonSerializer.Deserialize<PackageRepository>(repositoryString, SerializerOptions);

                if (repository?.Packages is not null)
                {
                    foreach (var manifest in repository.Packages)
                    {
                        try
                        {
                            if (manifest.ManifestUrl == null) continue;

                            var downloadManifest = await _httpService.DownloadTextAsync(manifest.ManifestUrl, cancellationToken: cancellationToken);
                            var package = JsonSerializer.Deserialize<Package>(downloadManifest!, SerializerOptions);

                            if (package != null)
                                packages.Add(package);
                        }
                        catch (Exception e)
                        {
                            _logger.Error(e.Message, e);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error(e.Message, e);
                return null;
            }
        }
        else
        {
            var package = JsonSerializer.Deserialize<Package>(repositoryString!, SerializerOptions);
            if (package != null) packages.Add(package);
        }

        return packages.ToArray();
    }

    private bool LoadInstalledPackagesDatabase()
    {
        try
        {
            if (File.Exists(_packageDataBasePath))
            {
                using var file = File.OpenRead(_packageDataBasePath);
                var installedPackages = JsonSerializer.Deserialize<InstalledPackage[]>(file, SerializerOptions);
                if (installedPackages != null)
                {
                    foreach (var installedPackage in installedPackages)
                    {
                        var package = new Package
                        {
                            Id = installedPackage.Id,
                            Type = installedPackage.Type,
                            Name = installedPackage.Name,
                            Category = installedPackage.Category,
                            Versions = [
                                new PackageVersion
                                {
                                    Version = installedPackage.InstalledVersion
                                }
                            ],
                            Description = installedPackage.Description,
                            License = installedPackage.License
                        };

                        AddPackage(package, installedPackage.InstalledVersion);
                    }
                }
            }

            foreach (var package in StandalonePackages)
            {
                if (Packages.TryGetValue(package.Id!, out var pkg))
                {
                    pkg.Package = package;
                    pkg.InstalledVersion = package.Versions?.FirstOrDefault(x => x.Version == pkg.InstalledVersion?.Version);
                }
                else
                {
                    AddPackage(package);
                }
            }
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return false;
        }

        return true;
    }

    private async Task SaveInstalledPackagesDatabaseAsync()
    {
        try
        {
            await using var file = File.OpenWrite(_packageDataBasePath);
            file.SetLength(0);

            var installedPackages = Packages
                .Where(x => x.Value.InstalledVersion is not null)
                .Select(x => new InstalledPackage(x.Value.Package.Id!, x.Value.Package.Type!, x.Value.Package.Name!,
                    x.Value.Package.Category, x.Value.Package.Description, x.Value.Package.License,
                    x.Value.InstalledVersion!.Version!))
                .ToArray();

            await JsonSerializer.SerializeAsync(file, installedPackages, SerializerOptions);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }

    private void ObserveInstall(Package package, Task<bool> installTask)
    {
        lock (_installLock)
        {
            _activeInstalls[package] = installTask;

            _ = installTask.ContinueWith(t =>
            {
                t.Wait();
                FinishInstall(package);
            }, TaskScheduler.Default);
        }
    }

    private void FinishInstall(Package package)
    {
        lock (_installLock)
        {
            _activeInstalls.Remove(package);
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MyRegex();
}
