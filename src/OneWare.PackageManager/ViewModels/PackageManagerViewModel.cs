using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
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

    private bool _showUpdate = true;

    public bool ShowUpdate
    {
        get => _showUpdate;
        set
        {
            SetProperty(ref _showUpdate, value);
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

    public PackageManagerViewModel(IPackageService packageService, ILogger logger)
    {
        _packageService = packageService;
        _logger = logger;
        
        PackageCategories.Add(new PackageCategoryViewModel("Plugins", Application.Current!.GetResourceObservable("BoxIcons.RegularExtension")));
        PackageCategories[0].SubCategories.Add(new PackageCategoryViewModel("Languages", Application.Current!.GetResourceObservable("FluentIcons.ProofreadLanguageRegular")));
        PackageCategories[0].SubCategories.Add(new PackageCategoryViewModel("Toolchains", Application.Current!.GetResourceObservable("FeatherIcons.Tool")));
        PackageCategories[0].SubCategories.Add(new PackageCategoryViewModel("Simulators", Application.Current!.GetResourceObservable("Material.Pulse")));
        PackageCategories[0].SubCategories.Add(new PackageCategoryViewModel("Boards", Application.Current!.GetResourceObservable("NiosIcon")));
        PackageCategories[0].SubCategories.Add(new PackageCategoryViewModel("Misc", Application.Current!.GetResourceObservable("Module")));
        PackageCategories.Add(new PackageCategoryViewModel("Libraries", Application.Current!.GetResourceObservable("BoxIcons.RegularLibrary")));
        PackageCategories.Add(new PackageCategoryViewModel("Binaries", Application.Current!.GetResourceObservable("BoxIcons.RegularCode")));
        
        SelectedCategory = PackageCategories.First();
        
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
            foreach (var sub in category.SubCategories)
            {
                foreach (var pkg in sub.Packages.ToArray())
                {
                    sub.Remove(pkg);
                }
            }
        }
        foreach (var (_, packageModel) in _packageService.Packages)
        {
            try
            {
                var model = ContainerLocator.Container.Resolve<PackageViewModel>((typeof(PackageModel), packageModel));

                var category = packageModel.Package.Type switch
                {
                    "Plugin" => PackageCategories[0],
                    "Library" => PackageCategories[1],
                    "NativeTool" => PackageCategories[2],
                    _ => null
                };

                if (category == null) continue;
                
                var subCategory = category.SubCategories.FirstOrDefault(x =>
                    x.Header.Equals(packageModel.Package.Category, StringComparison.OrdinalIgnoreCase));

                if (subCategory != null)
                {
                    subCategory.Add(model);
                }
                else
                {
                    category.Add(model);
                }
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
            categoryModel.Filter(Filter, _showInstalled, _showAvailable, _showUpdate);
        }
    }
}