using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.PackageManager.Views;

namespace OneWare.PackageManager.ViewModels;

public class PackageManagerViewModel : FlexibleWindowViewModelBase, IPackageWindowService
{
    private static readonly char[] CategorySeparators = ['/', '\\'];

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

        RegisterCategory("Plugins", new IconModel("BoxIcons.RegularExtension"));
        RegisterCategory("Plugins/Languages", new IconModel("FluentIcons.ProofreadLanguageRegular"));
        RegisterCategory("Plugins/Toolchains", new IconModel("FeatherIcons.Tool"));
        RegisterCategory("Plugins/Simulators", new IconModel("Material.Pulse"));
        RegisterCategory("Plugins/Tools", new IconModel("Module"));
        RegisterCategory("Hardware", new IconModel("NiosIcon"));
        RegisterCategory("Hardware/FPGA Boards");
        RegisterCategory("Hardware/Extensions");
        RegisterCategory("Libraries", new IconModel("BoxIcons.RegularLibrary"));
        RegisterCategory("Binaries", new IconModel("BoxIcons.RegularCode"));
        RegisterCategory("Binaries/ONNX Runtimes");
        RegisterCategory("Drivers", new IconModel("BoxIcons.RegularUsb"));

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

    public void RegisterCategory(string categoryPath, IconModel? iconModel = null)
    {
        var segments = SplitCategoryPath(categoryPath);
        if (segments.Length == 0) return;

        PackageCategoryViewModel? current = null;

        for (var i = 0; i < segments.Length; i++)
        {
            var header = NormalizeCategorySegment(segments[i], i == segments.Length - 1);
            var categories = current == null ? PackageCategories : current.SubCategories;

            var existing = FindCategory(categories, header);
            if (existing == null)
            {
                var category = new PackageCategoryViewModel(header, i == segments.Length - 1 ? iconModel : null);
                categories.Add(category);
                existing = category;
            }

            current = existing;
        }
    }

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
            ClearCategoryPackages(category);

        foreach (var (_, packageModel) in _packageService.Packages)
            try
            {
                var viewModel =
                    new PackageViewModel(packageModel, _packageService, _httpService, _windowService, _applicationStateService, _logger);

                var targetCategory = ResolveCategoryForPackage(packageModel.Package);
                if (targetCategory == null) continue;

                targetCategory.Add(viewModel);
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

    private static string[] SplitCategoryPath(string? categoryPath)
    {
        if (string.IsNullOrWhiteSpace(categoryPath)) return [];

        return categoryPath
            .Split(CategorySeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToArray();
    }

    private static string NormalizeCategorySegment(string segment, bool isLeaf)
    {
        if (isLeaf && segment.Equals("Misc", StringComparison.OrdinalIgnoreCase))
            return "Tools";

        return segment;
    }

    private static PackageCategoryViewModel? FindCategory(
        IEnumerable<PackageCategoryViewModel> categories,
        string header)
    {
        return categories.FirstOrDefault(x =>
            x.Header.Equals(header, StringComparison.OrdinalIgnoreCase));
    }

    private static PackageCategoryViewModel GetOrCreateCategory(
        IList<PackageCategoryViewModel> categories,
        string header,
        IconModel? iconModel = null)
    {
        var existing = FindCategory(categories, header);
        if (existing != null) return existing;

        var category = new PackageCategoryViewModel(header, iconModel);
        categories.Add(category);
        return category;
    }

    private PackageCategoryViewModel? ResolveCategoryForPackage(Essentials.PackageManager.Package package)
    {
        var rawCategory = package.Category;
        var hasPath = !string.IsNullOrWhiteSpace(rawCategory) &&
                      rawCategory.IndexOfAny(CategorySeparators) >= 0;

        if (hasPath)
        {
            var segments = SplitCategoryPath(rawCategory);
            if (segments.Length == 0) return ResolveRootCategoryForType(package.Type);

            var root = GetOrCreateCategory(PackageCategories, segments[0]);
            var current = root;

            for (var i = 1; i < segments.Length; i++)
            {
                var header = NormalizeCategorySegment(segments[i], i == segments.Length - 1);
                current = GetOrCreateCategory(current.SubCategories, header);
            }

            return current;
        }

        var category = ResolveRootCategoryForType(package.Type);
        if (category == null) return null;

        var wantedCategory = rawCategory;
        if (string.IsNullOrWhiteSpace(wantedCategory))
            return category;

        wantedCategory = NormalizeCategorySegment(wantedCategory, true);

        var subCategory = FindCategory(category.SubCategories, wantedCategory);
        if (subCategory == null)
        {
            subCategory = new PackageCategoryViewModel(wantedCategory);
            category.SubCategories.Add(subCategory);
        }

        return subCategory;
    }

    private PackageCategoryViewModel? ResolveRootCategoryForType(string? packageType)
    {
        if (string.IsNullOrWhiteSpace(packageType)) return null;

        var rootCategoryName = packageType switch
        {
            "Plugin" => "Plugins",
            "Hardware" => "Hardware",
            "Library" => "Libraries",
            "NativeTool" => "Binaries",
            "OnnxRuntime" => "Binaries",
            _ => null
        };

        if (rootCategoryName == null) return null;
        return FindCategory(PackageCategories, rootCategoryName);
    }

    private static void ClearCategoryPackages(PackageCategoryViewModel category)
    {
        foreach (var pkg in category.Packages.ToArray())
            category.Remove(pkg);

        foreach (var subCategory in category.SubCategories)
            ClearCategoryPackages(subCategory);
    }
}
