using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Enums;
using OneWare.PackageManager.Models;
using OneWare.PackageManager.Serializer;
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
    
    public Dictionary<string, PackageModel> Packages { get; } = [];

    public event EventHandler? UpdateStarted;
    
    public event EventHandler? UpdateEnded;
    
    public PackageService(IHttpService httpService, ILogger logger, IPaths paths)
    {
        _httpService = httpService;
        _logger = logger;
        
        PackageDataBasePath = Path.Combine(paths.PackagesDirectory, $"{paths.AppName.ToLower().Replace(" ", "")}-packages.json");
    }

    public void RegisterPackageRepository(string url)
    {
        PackageRepositories.Add(url);
    }
    
    private void RegisterPackage(Package package, string? installedVersion = null)
    {
        if (package.Id == null) throw new Exception("Package ID cannot be empty");
        
        if (Packages.TryGetValue(package.Id, out var existing))
        {
            existing.Package = package;
            return;
        }

        var model = package.Type switch
        {
            "Plugin" => ContainerLocator.Container.Resolve<PluginPackageModel>((typeof(Package), package)),
            _ => throw new Exception($"Package Type invalid/missing for {package.Name}!")
        };
        
        if (installedVersion != null)
        {
            model.InstalledVersion = package.Versions!.First(x => x.Version == installedVersion);
            model.Status = PackageStatus.Installed;
        }
        else
        {
            model.Status = PackageStatus.Available;
        }
        
        Packages.Add(package.Id, model);
        
        Observable.FromEventPattern(model, nameof(model.Installed))
            .Subscribe(x => _ = SaveInstalledPackagesDatabaseAsync())
            .DisposeWith(_packageRegistrationSubscription);
        
        Observable.FromEventPattern(model, nameof(model.Removed))
            .Subscribe(x => _ = SaveInstalledPackagesDatabaseAsync())
            .DisposeWith(_packageRegistrationSubscription);
    }
    
    public async Task<bool> LoadPackagesAsync()
    {
        if(_cancellationTokenSource is not null) await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource = new CancellationTokenSource();
        
        UpdateStarted?.Invoke(this, EventArgs.Empty);
        
        _packageRegistrationSubscription?.Dispose();
        _packageRegistrationSubscription = new CompositeDisposable();
        
        var removals = Packages.Where(x => x.Value.Status is PackageStatus.Available);
        foreach (var rm in removals)
        {
            Packages.Remove(rm.Key);
        }

        var result = await LoadInstalledPackagesDatabaseAsync(_cancellationTokenSource.Token);

        foreach (var repository in PackageRepositories)
        {
            var result2 = await LoadPackageRepositoryAsync(repository, _cancellationTokenSource.Token);
            result = result && result2;
        }

        UpdateEnded?.Invoke(this, EventArgs.Empty);
        
        return result;
    }
    
    private async Task<bool> LoadPackageRepositoryAsync(string url, CancellationToken cancellationToken)
    {
        var repositoryString = await _httpService.DownloadTextAsync(url,TimeSpan.FromSeconds(1), cancellationToken);
        
        try
        {
            if (repositoryString == null) throw new NullReferenceException(nameof(repositoryString));
            
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

                    if(package == null) continue;
                    
                    RegisterPackage(package);
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

    private async Task<bool> LoadInstalledPackagesDatabaseAsync(CancellationToken token)
    {
        try
        {
            if (File.Exists(PackageDataBasePath))
            {
                await using var file = File.OpenRead(PackageDataBasePath);
                var installedPackages = await JsonSerializer.DeserializeAsync<InstalledPackage[]>(file, SerializerOptions, token);
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
                            Versions = [
                                new PackageVersion()
                                {
                                    Version = installedPackage.InstalledVersion
                                }
                            ],
                            Description = installedPackage.Description,
                            License = installedPackage.License
                        };
                        
                        RegisterPackage(package, installedPackage.InstalledVersion);
                    }
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
                    x.Value.Package.Category, x.Value.Package.Description, x.Value.Package.License, x.Value.InstalledVersion!.Version!))
                .ToArray();

            await JsonSerializer.SerializeAsync(file, installedPackages, SerializerOptions);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }
}