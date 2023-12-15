using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.PackageManager.Enums;
using OneWare.PackageManager.Models;
using OneWare.PackageManager.Serializer;
using OneWare.SDK.Services;
using Prism.Ioc;

namespace OneWare.PackageManager.ViewModels;

public class PackageManagerViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        WriteIndented = true
    };
    
    private readonly IHttpService _httpService;
    private readonly ILogger _logger;
    private PackageCategoryModel? _selectedCategory;
    private CancellationTokenSource? _cancellationTokenSource;
    private CompositeDisposable _packageRegistrationSubscription = new();
    
    private string PackageDataBasePath { get; }

    private bool _showInstalled = true;
    public bool ShowInstalled
    {
        get => _showInstalled;
        set
        {
            SetProperty(ref _showInstalled, value);
            FilterPackages();
        }
    }

    private bool _showAvailable = true;
    public bool ShowAvailable
    {
        get => _showAvailable;
        set
        {
            SetProperty(ref _showAvailable, value); 
            FilterPackages();
        }
    }

    private string _filter = string.Empty;
    public string Filter
    {
        get => _filter;
        set
        {
            SetProperty(ref _filter, value);
            FilterPackages();
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public PackageCategoryModel? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }
    
    public ObservableCollection<PackageCategoryModel> PackageCategories { get; } = [];

    public Dictionary<string, PackageViewModel> Packages { get; } = new();
    
    public PackageManagerViewModel(IHttpService httpService, ILogger logger, IPaths paths)
    {
        _httpService = httpService;
        _logger = logger;

        PackageDataBasePath = Path.Combine(paths.PackagesDirectory, "oneware-packages.json");
        
        PackageCategories.Add(new PackageCategoryModel("All"));
        SelectedCategory = PackageCategories.First();

        RegisterPackageCategory(new PackageCategoryModel("Languages", Application.Current?.GetResourceObservable("FluentIcons.ProofreadLanguageRegular")));
        RegisterPackageCategory(new PackageCategoryModel("Toolchains", Application.Current?.GetResourceObservable("FeatherIcons.Tool")));
        RegisterPackageCategory(new PackageCategoryModel("Simulators", Application.Current?.GetResourceObservable("Material.Pulse")));
        RegisterPackageCategory(new PackageCategoryModel("Boards", Application.Current?.GetResourceObservable("NiosIcon")));
        RegisterPackageCategory(new PackageCategoryModel("Libraries", Application.Current?.GetResourceObservable("BoxIcons.RegularLibrary")));
        RegisterPackageCategory(new PackageCategoryModel("Misc", Application.Current?.GetResourceObservable("Module")));
        
        _ = LoadPackagesAsync();
    }

    private void FilterPackages()
    {
        foreach (var categoryModel in PackageCategories)
        {
            categoryModel.Filter(Filter, _showInstalled, _showAvailable);
        }
    }
    
    private void RegisterPackageCategory(PackageCategoryModel category)
    {
        PackageCategories.Add(category);
    }
    
    private void RegisterPackage(Package package, string? installedVersion = null)
    {
        if (package.Id == null) throw new Exception("Package ID cannot be empty");
        
        if (Packages.TryGetValue(package.Id, out var existing))
        {
            existing.Package = package;
            return;
        }

        try
        {
            var model = package.Type switch
            {
                "Plugin" => ContainerLocator.Container.Resolve<PluginPackageViewModel>((typeof(Package), package)),
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

            var category = PackageCategories.FirstOrDefault(x =>
                x.Header.Equals(package.Category, StringComparison.OrdinalIgnoreCase)) ?? PackageCategories.Last();
 
            if(category != PackageCategories.First())
                PackageCategories.First().Add(model);
            Packages.Add(package.Id, model);
            category.Add(model);
            FilterPackages();

            Observable.FromEventPattern(model, nameof(model.Installed))
                .Subscribe(x => _ = SaveInstalledPackagesDatabaseAsync())
                .DisposeWith(_packageRegistrationSubscription);
        
            Observable.FromEventPattern(model, nameof(model.Removed))
                .Subscribe(x => _ = SaveInstalledPackagesDatabaseAsync())
                .DisposeWith(_packageRegistrationSubscription);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }
    
    public async Task LoadPackagesAsync()
    {
        if(_cancellationTokenSource is not null) await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource = new CancellationTokenSource();
        
        _packageRegistrationSubscription?.Dispose();
        _packageRegistrationSubscription = new CompositeDisposable();
        
        IsLoading = true;
        var removals = Packages.Where(x => x.Value.Status is PackageStatus.Available);
        foreach (var rm in removals)
        {
            Packages.Remove(rm.Key);
            foreach (var category in PackageCategories)
            {
                category.Remove(rm.Value);
            }
        }

        await LoadInstalledPackagesDatabaseAsync(_cancellationTokenSource.Token);
        
        await LoadPackageRepositoryAsync(
            "https://raw.githubusercontent.com/ProtopSolutions/OneWare.PublicPackages/main/oneware-packages.json", _cancellationTokenSource.Token);
        
        FilterPackages();
        IsLoading = false;
    }
    
    private async Task LoadPackageRepositoryAsync(string url, CancellationToken cancellationToken)
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
        }
    }

    private async Task LoadInstalledPackagesDatabaseAsync(CancellationToken token)
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
        }
    }
    
    private async Task SaveInstalledPackagesDatabaseAsync()
    {
        try
        {
            await using var file = File.OpenWrite(PackageDataBasePath);
            file.SetLength(0);
            
            var installedPackages = PackageCategories.First().Packages
                .Where(x => x.Status == PackageStatus.Installed)
                .Select(x => new InstalledPackage(x.Package.Id!, x.Package.Type!, x.Package.Name!, x.Package.Category, x.Package.Description, x.Package.License, x.InstalledVersion!.Version!))
                .ToArray();

            await JsonSerializer.SerializeAsync(file, installedPackages, SerializerOptions);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }
}