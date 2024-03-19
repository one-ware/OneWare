using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;
using OneWare.PackageManager.Models;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Services;
using Prism.Ioc;

namespace OneWare.PackageManager.ViewModels;

public class PackageManagerViewModel : ObservableObject
{
    private readonly IPackageService _packageService;
    private readonly ILogger _logger;
    private PackageCategoryViewModel? _selectedCategory;

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

    public PackageCategoryViewModel? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    public ObservableCollection<PackageCategoryViewModel> PackageCategories { get; } = [];

    public PackageManagerViewModel(IPackageService packageService, ILogger logger, IPaths paths)
    {
        _packageService = packageService;
        _logger = logger;

        PackageCategories.Add(new PackageCategoryViewModel("All"));
        SelectedCategory = PackageCategories.First();

        RegisterPackageCategory(new PackageCategoryViewModel("Languages",
            Application.Current!.GetResourceObservable("FluentIcons.ProofreadLanguageRegular")));
        RegisterPackageCategory(new PackageCategoryViewModel("Toolchains",
            Application.Current!.GetResourceObservable("FeatherIcons.Tool")));
        RegisterPackageCategory(new PackageCategoryViewModel("Simulators",
            Application.Current!.GetResourceObservable("Material.Pulse")));
        RegisterPackageCategory(new PackageCategoryViewModel("Boards",
            Application.Current!.GetResourceObservable("NiosIcon")));
        RegisterPackageCategory(new PackageCategoryViewModel("Libraries",
            Application.Current!.GetResourceObservable("BoxIcons.RegularLibrary")));
        RegisterPackageCategory(new PackageCategoryViewModel("Binaries",
            Application.Current!.GetResourceObservable("BoxIcons.RegularCode")));
        RegisterPackageCategory(new PackageCategoryViewModel("Misc",
            Application.Current!.GetResourceObservable("Module")));
        
        ConstructPackageViewModels();
        
        packageService.UpdateStarted += (_, _) =>
        {
            ConstructPackageViewModels();
            IsLoading = true;
        };
        
        packageService.UpdateEnded += (_, _) =>
        {
            IsLoading = false;
            ConstructPackageViewModels();
        };
    }

    private void RegisterPackageCategory(PackageCategoryViewModel categoryView)
    {
        PackageCategories.Add(categoryView);
    }

    public async Task RefreshPackagesAsync()
    {
        await _packageService.LoadPackagesAsync();
    }

    private void ConstructPackageViewModels()
    {
        foreach (var category in PackageCategories)
        {
            foreach (var pkg in category.Packages.ToArray())
            {
                category.Remove(pkg);
            }
        }
        foreach (var (_, packageModel) in _packageService.Packages)
        {
            try
            {
                var model = ContainerLocator.Container.Resolve<PackageViewModel>((typeof(PackageModel), packageModel));

                var category = PackageCategories.FirstOrDefault(x =>
                    x.Header.Equals(packageModel.Package.Category, StringComparison.OrdinalIgnoreCase)) ?? PackageCategories.Last();

                if (category != PackageCategories.First())
                    PackageCategories.First().Add(model);

                category.Add(model);
            }
            catch (Exception e)
            {
                _logger.Error(e.Message, e);
            }
        }
        FilterPackages();
    }

    private void FilterPackages()
    {
        foreach (var categoryModel in PackageCategories)
        {
            categoryModel.Filter(Filter, _showInstalled, _showAvailable);
        }
    }
}