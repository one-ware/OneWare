using System.Formats.Asn1;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using Avalonia.Media;
using Avalonia.Threading;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Models;
using Prism.Ioc;

namespace OneWare.PackageManager.Services;

public class PackageService : IPackageService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private readonly IHttpService _httpService;
    private readonly ILogger _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private CompositeDisposable _packageRegistrationSubscription = new();

    private string PackageDataBasePath { get; }

    public List<string> PackageRepositories { get; } = [];

    private List<Package> StandalonePackages { get; } = [];

    public Dictionary<string, PackageModel> Packages { get; } = [];

    private readonly Dictionary<Package, Task<bool>> _activeInstalls = new();

    private readonly object _lock = new();

    public event EventHandler? UpdateStarted;

    public event EventHandler? UpdateEnded;

    public PackageService(IHttpService httpService, ILogger logger, IPaths paths)
    {
        _httpService = httpService;
        _logger = logger;

        PackageDataBasePath = Path.Combine(paths.PackagesDirectory,
            $"{paths.AppName.ToLower().Replace(" ", "")}-packages.json");

        LoadInstalledPackagesDatabase();
    }

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
            Dispatcher.UIThread.Post(() => UpdateStarted?.Invoke(this, EventArgs.Empty));
            AddPackage(package);
            Dispatcher.UIThread.Post(() => UpdateEnded?.Invoke(this, EventArgs.Empty));
        }
    }

    private void AddPackage(Package package, string? installedVersion = null)
    {
        if (package.Id == null) throw new Exception("Package ID cannot be empty");

        PackageModel model = package.Type switch
        {
            "Plugin" => ContainerLocator.Container.Resolve<PluginPackageModel>((typeof(Package), package)),
            "NativeTool" => ContainerLocator.Container.Resolve<NativeToolPackageModel>((typeof(Package), package)),
            _ => throw new Exception($"Package Type invalid/missing for {package.Name}!")
        };

        model.InstalledVersion = package.Versions?.FirstOrDefault(x => x.Version == installedVersion);

        Packages.Add(package.Id, model);

        Observable.FromEventPattern<Task<bool>>(model, nameof(model.Installing))
            .Subscribe(x => ObserveInstall((x.Sender as PackageModel)!.Package, x.EventArgs))
            .DisposeWith(_packageRegistrationSubscription);

        Observable.FromEventPattern(model, nameof(model.Installed))
            .Subscribe(x => _ = SaveInstalledPackagesDatabaseAsync())
            .DisposeWith(_packageRegistrationSubscription);

        Observable.FromEventPattern(model, nameof(model.Removed))
            .Subscribe(x => _ = SaveInstalledPackagesDatabaseAsync())
            .DisposeWith(_packageRegistrationSubscription);
    }

    private async Task WaitForInstallsAsync()
    {
        //Wait until all installs complete
        Task<bool>[] activeInstallTasks;
        lock (_lock)
        {
            activeInstallTasks = _activeInstalls.Select(x => x.Value).ToArray();
        }

        if (activeInstallTasks.Length != 0)
        {
            await Task.WhenAll(activeInstallTasks.ToArray());
        }
    }

    public async Task<bool> LoadPackagesAsync()
    {
        if (_cancellationTokenSource is not null) await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource = new CancellationTokenSource();

        await WaitForInstallsAsync();

        Dispatcher.UIThread.Post(() => UpdateStarted?.Invoke(this, EventArgs.Empty));

        _packageRegistrationSubscription?.Dispose();
        _packageRegistrationSubscription = new CompositeDisposable();

        Packages.Clear();

        var result = LoadInstalledPackagesDatabase();

        foreach (var repository in PackageRepositories)
        {
            var result2 = await LoadPackageRepositoryAsync(repository, _cancellationTokenSource.Token);
            result = result && result2;
        }

        Dispatcher.UIThread.Post(() => UpdateEnded?.Invoke(this, EventArgs.Empty));

        return result;
    }

    private async Task<bool> LoadPackageRepositoryAsync(string url, CancellationToken cancellationToken)
    {
        var repositoryString = await _httpService.DownloadTextAsync(url, TimeSpan.FromSeconds(10), cancellationToken);
        if (repositoryString == null) return false;

        try
        {
            var repository = JsonSerializer.Deserialize<PackageRepository>(repositoryString, SerializerOptions);

            if (repository is { Packages: not null })
            {
                foreach (var manifest in repository.Packages)
                {
                    if (manifest.ManifestUrl == null) continue;

                    var downloadManifest =
                        await _httpService.DownloadTextAsync(manifest.ManifestUrl,
                            cancellationToken: cancellationToken);

                    var package = JsonSerializer.Deserialize<Package>(downloadManifest!, SerializerOptions);

                    if (package == null) continue;

                    if (package.Id != null && Packages.TryGetValue(package.Id, out var pkg))
                    {
                        pkg.Package = package;
                        continue;
                    }

                    AddPackage(package);
                }
            }
            else throw new Exception("Packages empty");
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return false;
        }

        return true;
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
                {
                    foreach (var installedPackage in installedPackages)
                    {
                        var package = new Package()
                        {
                            Id = installedPackage.Id,
                            Type = installedPackage.Type,
                            Name = installedPackage.Name,
                            Category = installedPackage.Category,
                            Versions =
                            [
                                new PackageVersion()
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
                    pkg.InstalledVersion =
                        package.Versions?.FirstOrDefault(x => x.Version == pkg.InstalledVersion?.Version);
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
            _logger.Error(e.Message, e);
        }
    }
    

    /// <summary>
    /// Will install the package if not already installed
    /// Waits if another thread already started the installation
    /// </summary>
    public Task<bool> InstallAsync(Package package)
    {
        lock (_lock)
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
                        if (_activeInstalls.TryGetValue(model.Package, out var task))
                        {
                            return task;
                        }
                        return Task.FromResult(model.Status is PackageStatus.Installed or PackageStatus.UpdateAvailable);
                }
            }
        }
        return Task.FromResult(false);
    }
    
    private void ObserveInstall(Package package, Task<bool> installTask)
    {
        lock (_lock)
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
        lock (_lock)
        {
            _activeInstalls.Remove(package);
        }
    }
}