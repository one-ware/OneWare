using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.PackageManager.Views;

namespace OneWare.PackageManager.ViewModels;

public partial class PackageManagerViewModel : FlexibleWindowViewModelBase, IPackageWindowService, IDisposable
{
    private readonly CompositeDisposable _subscriptions = new();
    private bool _disposed;
    private const string AllCategoryHeader = "All";
    private static readonly char[] CategorySeparators = ['/', '\\'];

    private readonly IApplicationStateService _applicationStateService;
    private readonly IHttpService _httpService;
    private readonly ILogger _logger;
    private readonly IPackageService _packageService;
    private readonly IWindowService _windowService;

    private bool _showAvailable = true;
    private bool _showInstalled = true;
    private int _selectedFilterIndex;

    public PackageManagerViewModel(IPackageService packageService, IHttpService httpService, ILogger logger,
        IWindowService windowService,
        IApplicationStateService applicationStateService)
    {
        _packageService = packageService;
        _httpService = httpService;
        _windowService = windowService;
        _logger = logger;
        _applicationStateService = applicationStateService;
        _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); FilterPackages(); };

        RegisterCategory(AllCategoryHeader);
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

        SelectedCategory = GetAllCategory() ?? PackageCategories.FirstOrDefault();

        UpdateAllCommand = new AsyncRelayCommand(UpdateAllAsync, () => !IsLoading && !_packageService.Packages.Any(x => x.Value.Status == PackageStatus.Installing) && _packageService.Packages.Any(x =>
            x.Value.Status is PackageStatus.UpdateAvailable or PackageStatus.UpdateAvailablePrerelease));
        _subscriptions.Add(_packageService.WhenValueChanged(x => x.IsUpdating)
            .Subscribe(x =>
            {
                if (Dispatcher.UIThread.CheckAccess()) { if (!_disposed) IsLoading = x; }
                else Dispatcher.UIThread.Post(() => { if (!_disposed) IsLoading = x; });
            }));
        
        _subscriptions.Add(Observable.FromEventPattern(_packageService, nameof(_packageService.PackagesUpdated)).Subscribe(_ =>
        {
            // PackagesUpdated can be raised from a background thread
            Dispatcher.UIThread.Post(() =>
            {
                if (_disposed) return;
                ConstructPackageViewModels();
                UpdateAllCommand.NotifyCanExecuteChanged();
            });
        }));

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

    public bool ShowAvailable
    {
        get => _showAvailable;
        set
        {
            SetProperty(ref _showAvailable, value);
            FilterPackages();
        }
    }

    /// <summary>
    ///     Browse page: 0 = Discover, 1 = Installed, 2 = Updates.
    /// </summary>
    public int SelectedFilterIndex
    {
        get => _selectedFilterIndex;
        set
        {
            SetProperty(ref _selectedFilterIndex, value);
            _showInstalled = value is 0 or 1;
            _showAvailable = value is 0 or 2;
            FilterPackages();
        }
    }

    public string Filter
    {
        get;
        set
        {
            SetProperty(ref field, value);
            _searchTimer.Stop();
            _searchTimer.Start();
            OnPropertyChanged(nameof(ShowFeatured));
        }
    } = string.Empty;

    public bool IsLoading
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;
            UpdateAllCommand?.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public PackageCategoryViewModel? SelectedCategory
    {
        get;
        set { if (SetProperty(ref field, value) && value != null) CategoryFilter = value.Header; }
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
        RefreshCategoryFilters();
    }

    public async Task RefreshPackagesAsync()
    {
        try
        {
            SourceWarning = await _packageService.RefreshAsync(false) ? null : "Some sources could not be loaded. Installed packages remain available. Retry with Refresh.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not refresh package sources");
            SourceWarning = "Sources could not be loaded. Installed packages remain available. Retry with Refresh.";
        }
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
        if (pvm.InstallCommand.CanExecute(view)) await pvm.InstallCommand.ExecuteAsync(view);

        return true;
    }

    public Task<bool> QuickInstallPackageAsync(string packageId)
    {
        return QuickInstallPackageAsync(packageId, null);
    }
    
    public async Task<bool> QuickInstallPackageAsync(string packageId, Window? owner)
    {
        if (!_packageService.Packages.TryGetValue(packageId, out var packageModel)) return false;

        var quickInstallViewModel = new PackageQuickInstallViewModel(packageModel, _packageService, _windowService);

        var view = new PackageQuickInstallView
        {
            DataContext = quickInstallViewModel
        };

        await _windowService.ShowDialogAsync(view, owner);

        return quickInstallViewModel.Success;
    }

    public async Task<bool> ShowAndUpdateAllAsync()
    {
        SelectedPackage = null;
        SelectedFilterIndex = 2;
        ShowExtensionManager();

        await Task.Delay(100);
        
        return await UpdateAllAsync();
    }

    public async Task ResolveSelectedPackageTabsAsync()
    {
        if (SelectedPackage != null) await SelectedPackage.ResolveTabsAsync();
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
        CategoryFilter = subcategory == null ? category : $"{category}/{subcategory}";
        SelectedPackage = null;
        SelectedFilterIndex = 0;
        SelectedCategory.SelectedPackage = null;
        return true;
    }
    
    private async Task<PackageViewModel?> FocusPluginAsync(string packageId)
    {
        if (!_cache.TryGetValue(packageId, out var packageVm))
        {
            await _windowService.ShowMessageAsync("Package unavailable", $"{packageId} is not in the current catalog or installed packages. Check Sources and refresh.", MessageBoxIcon.Warning);
            return null;
        }
        SelectedPackage = packageVm;
        return packageVm;
    }

    private void ConstructPackageViewModels()
    {
        var allCategory = GetAllCategory();

        foreach (var category in PackageCategories)
            ClearCategoryPackages(category);

        foreach (var (_, packageModel) in _packageService.Packages)
            try
            {
                if (!_cache.TryGetValue(packageModel.Package.Id!, out var viewModel))
                {
                    viewModel = new PackageViewModel(packageModel, _packageService, _httpService, _windowService, _applicationStateService, _logger);
                    _cache[packageModel.Package.Id!] = viewModel;
                    packageModel.PropertyChanged += OnPackageChanged;
                }
                else if (!ReferenceEquals(viewModel.PackageState, packageModel))
                {
                    viewModel.PackageState.PropertyChanged -= OnPackageChanged;
                    viewModel.PackageState = packageModel;
                    packageModel.PropertyChanged += OnPackageChanged;
                }

                viewModel.RefreshMetadata();
                var targetCategory = ResolveCategoryForPackage(packageModel.Package);
                if (targetCategory == null) continue;

                if (allCategory != null && !ReferenceEquals(allCategory, targetCategory))
                    allCategory.Add(viewModel);

                targetCategory.Add(viewModel);
            }
            catch (Exception e)
            {
                _logger.Error(e.Message, e);
            }

        foreach (var id in _cache.Keys.Where(id => !_packageService.Packages.ContainsKey(id)).ToArray())
        {
            var old = _cache[id];
            old.PackageState.PropertyChanged -= OnPackageChanged;
            old.Dispose();
            _cache.Remove(id);
            if (SelectedPackage == old) SelectedPackage = null;
        }
        foreach (var vm in _cache.Values) vm.RefreshDependencies();
        FilterPackages();
    }

    private void FilterPackages()
    {
        if (_disposed) return;
        foreach (var categoryModel in PackageCategories)
            categoryModel.Filter(Filter, _showInstalled, _showAvailable);
        RefreshBrowse();
    }

    public void Dispose()
    {
        _disposed = true;
        _searchTimer.Stop();
        _subscriptions.Dispose();
        foreach (var vm in _cache.Values)
        {
            vm.PackageState.PropertyChanged -= OnPackageChanged;
            vm.Dispose();
        }
        _cache.Clear();
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
        var packages = _packageService.Packages.Values
            .Where(x => x.Status is PackageStatus.UpdateAvailable or PackageStatus.UpdateAvailablePrerelease)
            .Select(x => (State: x, Target: x.ResolveTargetVersion()))
            .Where(x => x.Target != null)
            .OrderBy(x => x.State.Package.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (packages.Count == 0) return true;
        var result = await PackageOperationReview.RunAsync(_packageService, _windowService,
            packages.Select(x => new PackageRequest(x.State.Package.Id!, x.Target!.Version, x.Target.IsPrerelease)).ToArray());
        UpdateAllCommand.NotifyCanExecuteChanged();
        return result.Status is Essentials.PackageManager.Compatibility.PackageInstallResultReason.Installed
            or Essentials.PackageManager.Compatibility.PackageInstallResultReason.AlreadyInstalled;
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

    private PackageCategoryViewModel? GetAllCategory()
    {
        return FindCategory(PackageCategories, AllCategoryHeader);
    }

    private static void ClearCategoryPackages(PackageCategoryViewModel category)
    {
        foreach (var pkg in category.Packages.ToArray())
            category.Remove(pkg);

        foreach (var subCategory in category.SubCategories)
            ClearCategoryPackages(subCategory);
    }
}
