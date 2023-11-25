using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using OneWare.PackageManager.Enums;
using OneWare.PackageManager.Models;
using OneWare.PackageManager.Serializer;
using OneWare.PackageManager.Serializer.Installed;
using OneWare.SDK.Services;
using Prism.Ioc;

namespace OneWare.PackageManager.ViewModels;

public class PackageManagerViewModel : ObservableObject
{
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
    
    public ObservableCollection<PackageCategoryModel> PackageCategories { get; } = new();
    
    public PackageManagerViewModel(IHttpService httpService, ILogger logger, IPaths paths)
    {
        _httpService = httpService;
        _logger = logger;

        PackageDataBasePath = Path.Combine(paths.PackagesDirectory, "oneware-packages.json");
        _ = LoadPackagesAsync();
    }

    public void RegisterPackageCategory(PackageCategoryModel category)
    {
        PackageCategories.Add(category);
    }
    
    public void RegisterPackage(PackageCategoryModel category, PackageViewModel packageView)
    {
        PackageCategories.First().Packages.Add(packageView);
        category.Packages.Add(packageView);

        Observable.FromEventPattern(packageView, nameof(packageView.Installed))
            .Subscribe(x => _ = SaveInstalledPackagesDatabaseAsync())
            .DisposeWith(_packageRegistrationSubscription);
        
        Observable.FromEventPattern(packageView, nameof(packageView.Removed))
            .Subscribe(x => _ = SaveInstalledPackagesDatabaseAsync())
            .DisposeWith(_packageRegistrationSubscription);
    }
    
    public async Task LoadPackagesAsync()
    {
        if(_cancellationTokenSource is not null) await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource = new CancellationTokenSource();
        
        _packageRegistrationSubscription?.Dispose();
        _packageRegistrationSubscription = new CompositeDisposable();
        
        IsLoading = true;
        PackageCategories.Clear();
        PackageCategories.Add(new PackageCategoryModel("All"));
        SelectedCategory = PackageCategories.First();

        RegisterPackageCategory(new PackageCategoryModel("Languages", Application.Current?.GetResourceObservable("FluentIcons.ProofreadLanguageRegular")));
        RegisterPackageCategory(new PackageCategoryModel("Toolchains", Application.Current?.GetResourceObservable("FeatherIcons.Tool")));
        RegisterPackageCategory(new PackageCategoryModel("Simulators", Application.Current?.GetResourceObservable("Material.Pulse")));
        RegisterPackageCategory(new PackageCategoryModel("Boards", Application.Current?.GetResourceObservable("NiosIcon")));
        RegisterPackageCategory(new PackageCategoryModel("Libraries", Application.Current?.GetResourceObservable("BoxIcons.RegularLibrary")));
        RegisterPackageCategory(new PackageCategoryModel("Misc", Application.Current?.GetResourceObservable("Module")));
        
        await LoadPackageRepositoryAsync(
            "https://raw.githubusercontent.com/ProtopSolutions/OneWare.PublicPackages/main/oneware-packages.json", _cancellationTokenSource.Token);
        
        FilterPackages();
        IsLoading = false;
    }

    public void FilterPackages()
    {
        foreach (var cat in PackageCategories)
        {
            cat.Filter(Filter, _showInstalled, _showAvailable);
        }
    }

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };

    private void AddPackage(Package package)
    {
        
        var model = package.Type switch
        {
            "Plugin" => ContainerLocator.Container.Resolve<PluginPackageViewModel>((typeof(Package), package)),
            _ => null
        };

        if (model == null)
        {
            throw new Exception($"Package Type invalid/missing for {package.Name}!");
        }

        var category = PackageCategories.FirstOrDefault(x =>
                x.Header.Equals(package.Category, StringComparison.OrdinalIgnoreCase)) ?? PackageCategories.Last();
        
        RegisterPackage(category, model);
    }
    
    private async Task LoadPackageRepositoryAsync(string url, CancellationToken cancellationToken)
    {
        var repositoryString = await _httpService.DownloadTextAsync(url,TimeSpan.FromSeconds(1), cancellationToken);
        
        try
        {
            if (repositoryString == null) throw new NullReferenceException(nameof(repositoryString));
            
            var repository = JsonSerializer.Deserialize<PackageRepository>(repositoryString, _serializerOptions);
            
            if (repository is { Packages: not null })
            {
                foreach (var manifest in repository.Packages)
                {
                    if (manifest.ManifestUrl == null) continue;
                    
                    var downloadManifest =
                        await _httpService.DownloadTextAsync(manifest.ManifestUrl,
                            cancellationToken: cancellationToken);
                    
                    var package = JsonSerializer.Deserialize<Package>(downloadManifest!, _serializerOptions);

                    if(package == null) continue;
                    
                    AddPackage(package);
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
                await using var file = File.OpenWrite(PackageDataBasePath);
                var installedPackages = await JsonSerializer.DeserializeAsync<InstalledPackage[]>(file, cancellationToken: token);
                if (installedPackages != null)
                {
                    foreach (var installedPackage in installedPackages)
                    {
                        AddPackage(installedPackage.Package);
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
                .Select(x => new InstalledPackage(x.Package, x.InstalledVersion!.Version!))
                .ToArray();

            await JsonSerializer.SerializeAsync<InstalledPackage[]>(file, installedPackages);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }
}