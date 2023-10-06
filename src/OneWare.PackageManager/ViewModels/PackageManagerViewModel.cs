using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.PackageManager.Models;
using OneWare.PackageManager.Serializer;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.PackageManager.ViewModels;

public class PackageManagerViewModel : ObservableObject
{
    private readonly IHttpService _httpService;
    private readonly ILogger _logger;
    private PackageCategoryModel? _selectedCategory;
    private CancellationTokenSource? _cancellationTokenSource;

    private bool _showInstalled = true;
    public bool ShowInstalled
    {
        get => _showInstalled;
        set => SetProperty(ref _showInstalled, value);
    }

    private bool _showAvailable = true;
    public bool ShowAvailable
    {
        get => _showAvailable;
        set => SetProperty(ref _showAvailable, value);
    }
    
    private bool _isLoading = false;
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

    public PackageManagerViewModel(IHttpService httpService, ILogger logger)
    {
        _httpService = httpService;
        _logger = logger;
        
        _ = LoadPackagesAsync();
    }

    public void RegisterPackageCategory(PackageCategoryModel category)
    {
        PackageCategories.Add(category);
    }
    
    public void RegisterPackage(PackageCategoryModel category, PackageViewModel packageView)
    {
        category.Packages.Add(packageView);
    }
    
    public async Task LoadPackagesAsync()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        
        IsLoading = true;
        SelectedCategory = null;
        PackageCategories.Clear();

        RegisterPackageCategory(new PackageCategoryModel("Languages", Application.Current?.GetResourceObservable("FluentIcons.ProofreadLanguageRegular")));
        RegisterPackageCategory(new PackageCategoryModel("Toolchains", Application.Current?.GetResourceObservable("FeatherIcons.Tool")));
        RegisterPackageCategory(new PackageCategoryModel("Simulators", Application.Current?.GetResourceObservable("Material.Pulse")));
        RegisterPackageCategory(new PackageCategoryModel("Boards", Application.Current?.GetResourceObservable("NiosIcon")));
        
        // RegisterPackage(PackageCategories.Last(),
        //     new PackageModel("Max1000 Support", "Support for MAX1000 Development Board", "The MAX1000 FPGA Development Board is the most inexpensive way to start with FPGAs and OneWare Studio",
        //         await _httpService.DownloadImageAsync("https://vhdplus.com/assets/images/max1000-fd95dd816b048068dd3d9ce70c0f67c0.png"), new List<LinkModel>
        //         {
        //             new("Docs","https://vhdplus.com/docs/components/max1000/"),
        //             new("Get this Product!", "https://shop.vhdplus.com/product/max1000/")
        //         })
        //     );
        //
        // RegisterPackage(PackageCategories.Last(),
        //     new PackageModel("VHDPlus WiFi Extension", "Support for VHDPlus WiFi Extension Board", "The WiFi Extensions make it easy to use your FPGA as an IoT controller. You have to take a cheap ESP-01 and plug it in the connector. Then you can use the FPGA as a programmer and USB interface for the ESP8266 together with the onboard buttons. And when you only have one CRUVI connector left, you can just plug a second extension like the SCD40 CRUVI module on top of the extension.",
        //         await _httpService.DownloadImageAsync("https://vhdplus.com/assets/images/Wifi_Top-8e711729300fc78fb5ed8e74b75c8914.png"), new List<LinkModel>
        //         {
        //             new("Docs","https://vhdplus.com/docs/components/wifi/"),
        //             new("Get this Product!", "https://shop.vhdplus.com/product/vhdplus-wifi-extension/")
        //         })
        // );

        RegisterPackageCategory(new PackageCategoryModel("Libraries", Application.Current?.GetResourceObservable("BoxIcons.RegularLibrary")));
        
        RegisterPackageCategory(new PackageCategoryModel("Misc", Application.Current?.GetResourceObservable("Module")));
        
        SelectedCategory = PackageCategories.First();
        
        await LoadPackageRepositoryAsync(
            "https://raw.githubusercontent.com/ProtopSolutions/OneWare.PublicPackages/main/oneware-packages.json", _cancellationTokenSource.Token);
        
        IsLoading = false;
    }

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };
    
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
                    
                    var model = ContainerLocator.Container.Resolve<PackageViewModel>((typeof(Package), package));

                    await model.ResolveAsync(cancellationToken);
                    
                    RegisterPackage(PackageCategories.FirstOrDefault(x => x.Header.Equals(package.Category, StringComparison.OrdinalIgnoreCase)) ?? PackageCategories.Last(), model);
                }
            }
            else throw new Exception("Packages empty");
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }
}