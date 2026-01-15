using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Binding;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.PackageManager.Views;
using Prism.Ioc;

namespace OneWare.PackageManager.ViewModels;

public class PackageManagerViewModel : FlexibleWindowViewModelBase, IPackageWindowService
{
    private readonly IPackageService _packageService;
    private readonly IWindowService _windowService;
    private readonly ILogger _logger;
    private readonly IApplicationStateService _applicationStateService;

    private string _filter = string.Empty;
    private bool _isLoading;
    private PackageCategoryViewModel? _selectedCategory;
    private bool _showAvailable = true;
    private bool _showInstalled = true;
    private bool _showUpdate = true;

    public PackageManagerViewModel(IPackageService packageService, ILogger logger, IWindowService windowService,
        IApplicationStateService applicationStateService)
    {
        _packageService = packageService;
        _windowService = windowService;
        _logger = logger;
        _applicationStateService = applicationStateService;

        PackageCategories.Add(new PackageCategoryViewModel("Plugins",
            Application.Current!.GetResourceObservable("BoxIcons.RegularExtension")));
        PackageCategories[0].SubCategories.Add(new PackageCategoryViewModel("Languages",
            Application.Current!.GetResourceObservable("FluentIcons.ProofreadLanguageRegular")));
        PackageCategories[0].SubCategories.Add(new PackageCategoryViewModel("Toolchains",
            Application.Current!.GetResourceObservable("FeatherIcons.Tool")));
        PackageCategories[0].SubCategories.Add(new PackageCategoryViewModel("Simulators",
            Application.Current!.GetResourceObservable("Material.Pulse")));
        PackageCategories[0].SubCategories
            .Add(new PackageCategoryViewModel("Tools", Application.Current!.GetResourceObservable("Module")));

        var hardwareCategory =
            new PackageCategoryViewModel("Hardware", Application.Current!.GetResourceObservable("NiosIcon"));
        hardwareCategory.SubCategories.Add(new PackageCategoryViewModel("FPGA Boards"));
        hardwareCategory.SubCategories.Add(new PackageCategoryViewModel("Extensions"));

        PackageCategories.Add(hardwareCategory);
        PackageCategories.Add(new PackageCategoryViewModel("Libraries",
            Application.Current!.GetResourceObservable("BoxIcons.RegularLibrary")));
        PackageCategories.Add(new PackageCategoryViewModel("Binaries",
            Application.Current!.GetResourceObservable("BoxIcons.RegularCode")));
        PackageCategories.Add(new PackageCategoryViewModel("Drivers",
            Application.Current!.GetResourceObservable("BoxIcons.RegularUsb")));

        SelectedCategory = PackageCategories.First();

        _packageService.WhenValueChanged(x => x.IsUpdating).Subscribe(x => { IsLoading = x; });

        Observable.FromEventPattern(_packageService, nameof(_packageService.PackagesUpdated)).Subscribe(_ =>
        {
            ConstructPackageViewModels();
        });

        ConstructPackageViewModels();
    }

    public bool ShowInstalled
    {
        get => _showInstalled;
        set
        {
            SetProperty(ref _showInstalled, value);
            FilterPackages();
        }
    }

    public bool ShowUpdate
    {
        get => _showUpdate;
        set
        {
            SetProperty(ref _showUpdate, value);
            FilterPackages();
        }
    }

    public bool ShowAvailable
    {
        get => _showAvailable;
        set
        {
            SetProperty(ref _showAvailable, value);
            FilterPackages();
        }
    }

    public string Filter
    {
        get => _filter;
        set
        {
            SetProperty(ref _filter, value);
            FilterPackages();
        }
    }

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

    public async Task RefreshPackagesAsync()
    {
        await _packageService.LoadPackagesAsync();
    }

    public Control ShowExtensionManager()
    {
        var view = new PackageManagerView
        {
            DataContext = this
        };
        _windowService.Show(view);

        return view;
    }

    public Control? ShowExtensionManager(string category, string? subcategory)
    {
        if (!FocusCategory(category, subcategory))
            return null;

        return ShowExtensionManager();
    }
    
    public async Task<bool> ShowExtensionManagerAndTryInstallAsync(string packageId)
    {
        var category = PackageCategories.FirstOrDefault(x => x.VisiblePackages.Any(x => x.PackageModel.Package.Id == packageId));
        
        if (await FocusPluginAsync(category!.Header, packageId) is not { } pvm)
            return false;

        var view = ShowExtensionManager();
        await pvm.InstallCommand.ExecuteAsync(view);

        return true;
    }

    public async Task<bool> QuickInstallPackageAsync(string packageId)
    {
        var packageModel = _packageService.Packages.GetValueOrDefault(packageId);

        if (packageModel == null)
        {
            return false;
        }

        var quickInstallViewModel = new PackageQuickInstallViewModel(packageModel, _packageService);
        
        var view = new PackageQuickInstallView()
        {
            DataContext = quickInstallViewModel
        };
        
        await _windowService.ShowDialogAsync(view);

        return quickInstallViewModel.Success;
    }

    private bool FocusCategory(string category, string? subcategory)
    {
        PackageCategoryViewModel? categoryVm = PackageCategories
            .FirstOrDefault(x => x.Header == category);

        if (categoryVm == null)
            return false;

        if (subcategory != null)
        {
            categoryVm = categoryVm.SubCategories.FirstOrDefault(x => x.Header == subcategory);
            if (categoryVm == null)
                return false;
        }

        SelectedCategory = categoryVm;
        SelectedCategory.SelectedPackage = null;
        return true;
    }

    private async Task<PackageViewModel?> FocusPluginAsync(string category, string packageId)
    {
        PackageCategoryViewModel? categoryVm = PackageCategories
            .FirstOrDefault(x => x.Header == category);

        if (categoryVm != null && _packageService.Packages.TryGetValue(packageId, out PackageModel? packageModel))
        {
            PackageViewModel? packageVm = categoryVm.VisiblePackages
                .FirstOrDefault(x => x.PackageModel == packageModel);

            if (packageVm == null)
                return null;

            SelectedCategory = categoryVm;
            SelectedCategory.SelectedPackage = packageVm;

            await packageVm.ResolveTabsAsync();
            return packageVm;
        }

        return null;
    }

    private void ConstructPackageViewModels()
    {
        foreach (var category in PackageCategories)
        {
            foreach (var pkg in category.Packages.ToArray()) category.Remove(pkg);
            foreach (var sub in category.SubCategories)
            foreach (var pkg in sub.Packages.ToArray())
                sub.Remove(pkg);
        }

        foreach (var (_, packageModel) in _packageService.Packages)
            try
            {
                var model = ContainerLocator.Container.Resolve<PackageViewModel>((typeof(PackageModel), packageModel));

                var category = packageModel.Package.Type switch
                {
                    "Plugin" => PackageCategories[0],
                    "Hardware" => PackageCategories[1],
                    "Library" => PackageCategories[2],
                    "NativeTool" => PackageCategories[3],
                    _ => null
                };

                if (category == null) continue;

                var wantedCategory = packageModel.Package.Category;

                if (wantedCategory is "Misc") wantedCategory = "Tools";

                var subCategory = category.SubCategories.FirstOrDefault(x =>
                    x.Header.Equals(wantedCategory, StringComparison.OrdinalIgnoreCase));

                if (subCategory == null && wantedCategory != null)
                {
                    subCategory = new PackageCategoryViewModel(wantedCategory);
                    category.SubCategories.Add(subCategory);
                }

                subCategory?.Add(model);

                if (subCategory == null)
                    category.Add(model);
            }
            catch (Exception e)
            {
                _logger.Error(e.Message, e);
            }

        FilterPackages();
    }

    private void FilterPackages()
    {
        foreach (var categoryModel in PackageCategories)
            categoryModel.Filter(Filter, _showInstalled, _showAvailable, _showUpdate);
    }

    public override bool OnWindowClosing(FlexibleWindow window)
    {
        var needRestart = _packageService.Packages.Any(x => x.Value.Status == PackageStatus.NeedRestart);

        if (needRestart && AskForRestart)
        {
            _ = AskForRestartAsync(window.Host);
            return false;
        }

        AskForRestart = true;
        return base.OnWindowClosing(window);
    }

    public bool AskForRestart { get; set; } = true;

    private async Task AskForRestartAsync(Window? window)
    {
        var result = await _windowService.ShowYesNoCancelAsync(
            "Restart Required",
            "Some changes to installed packages or plugins require a restart to take effect. Do you want to restart now?",
            MessageBoxIcon.Warning, window);

        if (result == MessageBoxStatus.Yes)
        {
            AskForRestart = false;
            _applicationStateService.TryRestart();
        }
        else if (result == MessageBoxStatus.No)
        {
            AskForRestart = false;
            window?.Close();
        }
    }
}