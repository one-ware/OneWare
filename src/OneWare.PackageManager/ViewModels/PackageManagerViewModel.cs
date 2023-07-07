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

    public ObservableCollection<PackageModel> Packages { get; } = new();

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
        Packages.Add(package);
    }
    
    public async Task LoadPackagesAsync()
    {
        RegisterPackageCategory(new PackageCategoryModel("Languages", Application.Current?.GetResourceObservable("FluentIcons.ProofreadLanguageRegular")));
        RegisterPackageCategory(new PackageCategoryModel("Toolchains", Application.Current?.GetResourceObservable("FeatherIcons.Tool")));

        var download = await _httpService.HttpClient.GetAsync(
            "https://vhdplus.com/assets/images/max1000-fd95dd816b048068dd3d9ce70c0f67c0.png");

        var image = new Bitmap(await download.Content.ReadAsStreamAsync());
        
                
        var download2 = await _httpService.HttpClient.GetAsync(
            "https://vhdplus.com/assets/images/Wifi_Top-8e711729300fc78fb5ed8e74b75c8914.png");

        var image2 = new Bitmap(await download2.Content.ReadAsStreamAsync());

        RegisterPackageCategory(new PackageCategoryModel("Boards", Application.Current?.GetResourceObservable("NiosIcon")));

        RegisterPackage(PackageCategories.Last(),
            new PackageModel("Max1000 Support", "Support for MAX1000 Development Board", "The MAX1000 FPGA Development Board is the most inexpensive way to start with FPGAs and OneWare Studio",
                image, new List<LinkModel>
                {
                    new("Docs","https://vhdplus.com/docs/components/max1000/"),
                    new("Get this Product!", "https://shop.vhdplus.com/product/max1000/")
                })
            );
        
        RegisterPackage(PackageCategories.Last(),
            new PackageModel("VHDPlus WiFi Extension", "Support for VHDPlus WiFi Extension Board", "The WiFi Extensions make it easy to use your FPGA as an IoT controller. You have to take a cheap ESP-01 and plug it in the connector. Then you can use the FPGA as a programmer and USB interface for the ESP8266 together with the onboard buttons. And when you only have one CRUVI connector left, you can just plug a second extension like the SCD40 CRUVI module on top of the extension.",
                image2, new List<LinkModel>
                {
                    new("Docs","https://vhdplus.com/docs/components/wifi/"),
                    new("Get this Product!", "https://shop.vhdplus.com/product/vhdplus-wifi-extension/")
                })
        );

        RegisterPackageCategory(new PackageCategoryModel("Libraries", Application.Current?.GetResourceObservable("BoxIcons.RegularLibrary")));
        SelectedCategory = PackageCategories.First();
    }
}