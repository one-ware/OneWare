using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using OneWare.PackageManager.Models;
using OneWare.Shared.Services;

namespace OneWare.PackageManager.ViewModels;

public class PackageManagerViewModel : ObservableObject
{
    private readonly IHttpService _httpService;
    private PackageCategoryModel? _selectedCategory;

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

    public PackageManagerViewModel(IHttpService httpService)
    {
        _httpService = httpService;
        
        _ = LoadPackagesAsync();
    }

    public void RegisterPackageCategory(PackageCategoryModel category)
    {
        PackageCategories.Add(category);
    }
    
    public void RegisterPackage(PackageCategoryModel category, PackageModel package)
    {
        category.Packages.Add(package);
    }
    
    public async Task LoadPackagesAsync()
    {
        IsLoading = true;
        SelectedCategory = null;
        PackageCategories.Clear();

        RegisterPackageCategory(new PackageCategoryModel("Languages", Application.Current?.GetResourceObservable("FluentIcons.ProofreadLanguageRegular")));
        RegisterPackageCategory(new PackageCategoryModel("Toolchains", Application.Current?.GetResourceObservable("FeatherIcons.Tool")));
        RegisterPackageCategory(new PackageCategoryModel("Boards", Application.Current?.GetResourceObservable("NiosIcon")));

        RegisterPackage(PackageCategories.Last(),
            new PackageModel("Max1000 Support", "Support for MAX1000 Development Board", "The MAX1000 FPGA Development Board is the most inexpensive way to start with FPGAs and OneWare Studio",
                await _httpService.DownloadImageAsync("https://vhdplus.com/fassets/images/max1000-fd95dd816b048068dd3d9ce70c0f67c0.png"), new List<LinkModel>
                {
                    new("Docs","https://vhdplus.com/docs/components/max1000/"),
                    new("Get this Product!", "https://shop.vhdplus.com/product/max1000/")
                })
            );
        
        RegisterPackage(PackageCategories.Last(),
            new PackageModel("VHDPlus WiFi Extension", "Support for VHDPlus WiFi Extension Board", "The WiFi Extensions make it easy to use your FPGA as an IoT controller. You have to take a cheap ESP-01 and plug it in the connector. Then you can use the FPGA as a programmer and USB interface for the ESP8266 together with the onboard buttons. And when you only have one CRUVI connector left, you can just plug a second extension like the SCD40 CRUVI module on top of the extension.",
                await _httpService.DownloadImageAsync("https://vhdplus.com/fassets/images/Wifi_Top-8e711729300fc78fb5ed8e74b75c8914.png"), new List<LinkModel>
                {
                    new("Docs","https://vhdplus.com/docs/components/wifi/"),
                    new("Get this Product!", "https://shop.vhdplus.com/product/vhdplus-wifi-extension/")
                })
        );

        RegisterPackageCategory(new PackageCategoryModel("Libraries", Application.Current?.GetResourceObservable("BoxIcons.RegularLibrary")));
        SelectedCategory = PackageCategories.First();

        IsLoading = false;
    }
}