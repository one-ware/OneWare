using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Models;
using Prism.Ioc;

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

    private readonly Lock _installLock = new();
    
    private CancellationTokenSource? _cancellationTokenSource;

    public PackageService(IHttpService httpService, ISettingsService settingsService, IPaths paths)
    {
        _httpService = httpService;
        _settingsService = settingsService;

        PackageDataBasePath = Path.Combine(paths.PackagesDirectory,
            $"{paths.AppName.ToLower().Replace(" ", "")}-packages.json");

        LoadInstalledPackagesDatabase();
    }
    
    public bool IsUpdating
    {
        get => _isUpdating;
        private set => SetProperty(ref _isUpdating, value);
    }

    public event EventHandler? PackagesUpdated;

    private string PackageDataBasePath { get; }

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


    /// <summary>
    ///     Will install the package if not already installed
    ///     Waits if another thread already started the installation
    /// </summary>
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
                        string infoMsg = $"Downloading {model.Package.Name}...";
                        AppServices.Logger.LogInformation(infoMsg);
                        UserNotification.NewInformation(infoMsg)
                            .ViaOutput(Brushes.DarkCyan)
                            .Send();
                        
                        return model.DownloadAsync(model.Package.Versions.Last());
                    case PackageStatus.Installing:
                        if (_activeInstalls.TryGetValue(model.Package, out var task)) return task;
                        return Task.FromResult(model.Status is PackageStatus.Installed
                            or PackageStatus.UpdateAvailable);
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
            "Plugin" => ContainerLocator.Container.Resolve<PluginPackageModel>((typeof(Package), package)),
            "NativeTool" => ContainerLocator.Container.Resolve<NativeToolPackageModel>((typeof(Package), package)),
            "Hardware" => ContainerLocator.Container.Resolve<HardwarePackageModel>((typeof(Package), package)),
            "Library" => ContainerLocator.Container.Resolve<LibraryPackageModel>((typeof(Package), package)),
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
        //Wait until all installs complete
        Task<bool>[] activeInstallTasks;
        lock (_installLock)
        {
            activeInstallTasks = _activeInstalls.Select(x => x.Value).ToArray();
        }

        if (activeInstallTasks.Length != 0) await Task.WhenAll(activeInstallTasks.ToArray());
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

                if (repository is { Packages: not null })
                    foreach (var manifest in repository.Packages)
                    {
                        try
                        {
                            if (manifest.ManifestUrl == null) continue;

                            var downloadManifest =
                                await _httpService.DownloadTextAsync(manifest.ManifestUrl,
                                    cancellationToken: cancellationToken);

                            var package = JsonSerializer.Deserialize<Package>(downloadManifest!, SerializerOptions);

                            if (package == null) continue;

                            packages.Add(package);
                        }
                        catch (Exception e)
                        {
                            AppServices.Logger.LogError(e, e.Message);
                        }
                    }
                else throw new Exception("Packages empty");
            }
            catch (Exception e)
            {
                AppServices.Logger.LogError(e, e.Message);
                return null;
            }
        }
        else
        {
            //In case link is a plugin manifest directly
            var package = JsonSerializer.Deserialize<Package>(repositoryString!, SerializerOptions);

            if (package == null) return null;

            packages.Add(package);
        }

        return packages.ToArray();
    }

    private bool LoadInstalledPackagesDatabase()
    {
        try
        {
            if (File.Exists(PackageDataBasePath))
            {
                using var file = File.OpenRead(PackageDataBasePath);
                var installedPackages = JsonSerializer.Deserialize<InstalledPackage[]>(file, SerializerOptions);
                if (installedPackages != null)
                    foreach (var installedPackage in installedPackages)
                    {
                        var package = new Package
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

                        AddPackage(package, installedPackage.InstalledVersion);
                    }
            }

            foreach (var package in StandalonePackages)
                if (Packages.TryGetValue(package.Id!, out var pkg))
                {
                    pkg.Package = package;
                    pkg.InstalledVersion =
                        package.Versions?.FirstOrDefault(x => x.Version == pkg.InstalledVersion?.Version);
                }
                else
                {
                    AddPackage(package);
                }
        }
        catch (Exception e)
        {
            AppServices.Logger.LogError(e, e.Message);
            return false;
        }

        return true;
    }

    private async Task SaveInstalledPackagesDatabaseAsync()
    {
        try
        {
            await using var file = File.OpenWrite(PackageDataBasePath);
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
            AppServices.Logger.LogError(e, e.Message);
        }
    }

    private void ObserveInstall(Package package, Task<bool> installTask)
    {
        lock (_installLock)
        {
            _activeInstalls.Add(package, installTask);

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