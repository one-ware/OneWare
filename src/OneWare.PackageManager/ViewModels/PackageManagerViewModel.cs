using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.PackageManager.Views;

namespace OneWare.PackageManager.ViewModels;

public class PackageManagerViewModel : FlexibleWindowViewModelBase, IPackageWindowService
{
    private readonly IApplicationStateService _applicationStateService;
    private readonly IHttpService _httpService;
    private readonly ILogger _logger;
    private readonly IPackageService _packageService;
    private readonly IWindowService _windowService;

    private bool _showAvailable = true;
    private bool _showInstalled = true;
    private bool _showUpdate = true;

    public PackageManagerViewModel(IPackageService packageService, IHttpService httpService, ILogger logger,
        IWindowService windowService,
        IApplicationStateService applicationStateService)
    {
        _packageService = packageService;
        _httpService = httpService;
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

        UpdateAllCommand = new AsyncRelayCommand(UpdateAllAsync, () => _packageService.Packages.Any(x => x.Value.Status == PackageStatus.UpdateAvailable));
        
        Observable.FromEventPattern(_packageService, nameof(_packageService.PackagesUpdated)).Subscribe(_ =>
        {
            ConstructPackageViewModels();
            UpdateAllCommand.NotifyCanExecuteChanged();
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
        get;
        set
        {
            SetProperty(ref field, value);
            FilterPackages();
        }
    } = string.Empty;

    public bool IsLoading
    {
        get;
        set => SetProperty(ref field, value);
    }

    public PackageCategoryViewModel? SelectedCategory
    {
        get;
        set => SetProperty(ref field, value);
    }

    public ObservableCollection<PackageCategoryViewModel> PackageCategories { get; } = [];

    public bool AskForRestart { get; set; } = true;
    
    public AsyncRelayCommand UpdateAllCommand { get; }

    public async Task RefreshPackagesAsync()
    {
        await _packageService.RefreshAsync();
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

    public async Task<bool> ShowExtensionManagerAsync(string packageId)
    {
        if (await FocusPluginAsync(packageId) is not { } pvm)
            return false;
        
        ShowExtensionManager();

        return true;
    }

    public async Task<bool> ShowExtensionManagerAndTryInstallAsync(string packageId)
    {
        if (await FocusPluginAsync(packageId) is not { } pvm)
            return false;

        var view = ShowExtensionManager();
        await pvm.InstallCommand.ExecuteAsync(view);

        return true;
    }

    public async Task<bool> QuickInstallPackageAsync(string packageId)
    {
        if (!_packageService.Packages.TryGetValue(packageId, out var packageModel)) return false;

        var quickInstallViewModel = new PackageQuickInstallViewModel(packageModel, _packageService);

        var view = new PackageQuickInstallView
        {
            DataContext = quickInstallViewModel
        };

        await _windowService.ShowDialogAsync(view);

        return quickInstallViewModel.Success;
    }

    private bool FocusCategory(string category, string? subcategory)
    {
        var categoryVm = PackageCategories
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
    
    private async Task<PackageViewModel?> FocusPluginAsync(string packageId)
    {
        var categoryVm =
            PackageCategories.FirstOrDefault(x => x.VisiblePackages.Any(x => x.PackageState.Package.Id == packageId));

        if (categoryVm != null && _packageService.Packages.TryGetValue(packageId, out var packageModel))
        {
            var packageVm = categoryVm.VisiblePackages
                .FirstOrDefault(x => x.PackageState == packageModel);

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
                var viewModel =
                    new PackageViewModel(packageModel, _packageService, _httpService, _windowService, _applicationStateService, _logger);

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

                subCategory?.Add(viewModel);

                if (subCategory == null)
                    category.Add(viewModel);
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

    private async Task AskForRestartAsync(Window? window)
    {
        var result = await _windowService.ShowYesNoCancelAsync(
            "Restart Required",
            "Some changes to installed packages or plugins require a restart to take effect. Do you want to restart now?",
            MessageBoxIcon.Warning, window);

        if (result == MessageBoxStatus.Yes)
        {
            AskForRestart = false;
            _ = _applicationStateService.TryRestartAsync();
        }
        else if (result == MessageBoxStatus.No)
        {
            AskForRestart = false;
            window?.Close();
        }
    }

    public async Task<bool> UpdateAllAsync()
    {
        var packages = _packageService.Packages.Values.Where(x => x.Status == PackageStatus.UpdateAvailable).ToList();
        
        foreach (var package in packages)
        {
            await FocusPluginAsync(package.Package!.Id!);
            await _packageService.UpdateAsync(package.Package.Id!, null, false, true);
        }
        
        UpdateAllCommand.NotifyCanExecuteChanged();
        
        return true;
    }
}
