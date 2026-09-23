using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
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

public class PackageManagerViewModel : FlexibleWindowViewModelBase, IPackageWindowService
{
    private const string AllCategoryHeader = "All";
    private const string UpdatesCategoryHeader = "Updates";

    /// <summary>
    ///     The package promoted by the hero banner above the list.
    /// </summary>
    private const string FeaturedPackageId = "OneWare.AI";

    private static readonly char[] CategorySeparators = ['/', '\\'];

    private readonly IApplicationStateService _applicationStateService;
    private readonly IHttpService _httpService;
    private readonly ILogger _logger;
    private readonly IPackageService _packageService;
    private readonly IWindowService _windowService;

    private readonly PackageCategoryViewModel _allCategory;
    private readonly PackageCategoryViewModel _updatesCategory;

    private readonly Dictionary<string, PackageViewModel> _packageViewModels = new();

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

        _allCategory = new PackageCategoryViewModel(AllCategoryHeader, PackageCategoryKind.Root);
        PackageCategories.Add(_allCategory);

        _updatesCategory = _allCategory.GetOrCreateSubCategory(UpdatesCategoryHeader, PackageCategoryKind.Updates);

        FeaturedPackage = new FeaturedPackageViewModel(
            "ONE AI is available",
            "Manage your ONE AI projects directly in the IDE and get annotation, model training and real-time camera checking.",
            "AI_Img",
            "https://one-ware.com/one-ai",
            new AsyncRelayCommand(ShowFeaturedPackageDetailsAsync));

        RegisterCategory("Plugins");
        RegisterCategory("Plugins/Languages");
        RegisterCategory("Plugins/Toolchains");
        RegisterCategory("Plugins/Simulators");
        RegisterCategory("Plugins/Tools");
        RegisterCategory("Hardware");
        RegisterCategory("Hardware/FPGA Boards");
        RegisterCategory("Hardware/Extensions");
        RegisterCategory("Libraries");
        RegisterCategory("Binaries");
        RegisterCategory("Binaries/ONNX Runtimes");
        RegisterCategory("Drivers");

        SelectedCategory = _allCategory;

        _packageService.WhenValueChanged(x => x.IsUpdating)
            .Subscribe(x => Dispatcher.UIThread.Post(() => IsLoading = x));

        UpdateAllCommand = new AsyncRelayCommand(UpdateAllAsync, () => _packageService.Packages.Any(x =>
            x.Value.Status is PackageStatus.UpdateAvailable or PackageStatus.UpdateAvailablePrerelease));
        
        Observable.FromEventPattern(_packageService, nameof(_packageService.PackagesUpdated)).Subscribe(_ =>
        {
            // PackagesUpdated can be raised from a background thread
            Dispatcher.UIThread.Post(() =>
            {
                ConstructPackageViewModels();
                UpdateAllCommand.NotifyCanExecuteChanged();
            });
        });

        ConstructPackageViewModels();
        Relayout();
    }

    public bool ShowInstalled
    {
        get => _showInstalled;
        set
        {
            SetProperty(ref _showInstalled, value);
            Relayout();
        }
    }

    public bool ShowAvailable
    {
        get => _showAvailable;
        set
        {
            SetProperty(ref _showAvailable, value);
            Relayout();
        }
    }

    /// <summary>
    ///     Index of the segmented control filter: 0 = All, 1 = Installed only, 2 = Available only.
    /// </summary>
    public int SelectedFilterIndex
    {
        get => _selectedFilterIndex;
        set
        {
            SetProperty(ref _selectedFilterIndex, value);
            _showInstalled = value is 0 or 1;
            _showAvailable = value is 0 or 2;
            Relayout();
        }
    }

    public string Filter
    {
        get;
        set
        {
            SetProperty(ref field, value);
            Relayout();
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

    /// <summary>
    ///     Pinned above the list, never part of it, so promoting a package cannot shift any row.
    /// </summary>
    public FeaturedPackageViewModel FeaturedPackage { get; }

    public bool AskForRestart { get; set; } = true;
    
    public AsyncRelayCommand UpdateAllCommand { get; }

    /// <summary>
    ///     Registers a category. <paramref name="iconModel" /> is accepted for API compatibility but no
    ///     longer used, categories are listed without icons.
    /// </summary>
    public void RegisterCategory(string categoryPath, IconModel? iconModel = null)
    {
        var segments = SplitCategoryPath(categoryPath);
        if (segments.Length == 0) return;

        var current = _allCategory;

        foreach (var (segment, index) in segments.Select((x, i) => (x, i)))
            current = current.GetOrCreateSubCategory(
                NormalizeCategorySegment(segment, index == segments.Length - 1));
    }

    public async Task RefreshPackagesAsync()
    {
        await _packageService.RefreshAsync(false);
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

    public Task<bool> QuickInstallPackageAsync(string packageId)
    {
        return QuickInstallPackageAsync(packageId, null);
    }
    
    public async Task<bool> QuickInstallPackageAsync(string packageId, Window? owner)
    {
        if (!_packageService.Packages.TryGetValue(packageId, out var packageModel)) return false;

        var quickInstallViewModel = new PackageQuickInstallViewModel(packageModel, _packageService);

        var view = new PackageQuickInstallView
        {
            DataContext = quickInstallViewModel
        };

        await _windowService.ShowDialogAsync(view, owner);

        return quickInstallViewModel.Success;
    }

    public async Task<bool> ShowAndUpdateAllAsync()
    {
        ShowExtensionManager();

        await Task.Delay(100);
        
        return await UpdateAllAsync();
    }

    public async Task ResolveSelectedPackageTabsAsync()
    {
        if (SelectedCategory?.SelectedPackage == null)
            return;
        
        await SelectedCategory.SelectedPackage.ResolveTabsAsync();
    }

    private bool FocusCategory(string category, string? subcategory)
    {
        var categoryVm = _allCategory.SubCategories
            .FirstOrDefault(x => x.Kind == PackageCategoryKind.Normal && x.Header == category);

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
        var categoryVm = _allCategory.SubCategories
                             .FirstOrDefault(x => x.Kind == PackageCategoryKind.Normal &&
                                                  x.VisiblePackages.Any(y => y.PackageState.Package.Id == packageId))
                         ?? _allCategory;

        if (categoryVm != null && _packageService.Packages.TryGetValue(packageId, out var packageModel))
        {
            var packageVm = categoryVm.VisiblePackages
                .FirstOrDefault(x => x.PackageState == packageModel);

            if (packageVm == null)
                return null;

            SelectedCategory = categoryVm;
            SelectedCategory.SelectedPackage = packageVm;

            _ = packageVm.ResolveIconAsync();
            await packageVm.ResolveTabsAsync();
            return packageVm;
        }

        return null;
    }

    /// <summary>
    ///     The banner ignores the search text and the segment filter, so the promoted package is not
    ///     necessarily laid out. The query is reset first, otherwise focusing it would silently do nothing.
    /// </summary>
    private async Task ShowFeaturedPackageDetailsAsync()
    {
        if (SelectedFilterIndex != 0) SelectedFilterIndex = 0;
        if (!string.IsNullOrEmpty(Filter)) Filter = string.Empty;

        await FocusPluginAsync(FeaturedPackageId);
    }

    private void ConstructPackageViewModels()    {
        foreach (var category in PackageCategories)
            ClearCategoryPackages(category);

        var staleIds = _packageViewModels.Keys
            .Where(x => !_packageService.Packages.ContainsKey(x))
            .ToList();

        foreach (var staleId in staleIds)
        {
            if (!_packageViewModels.Remove(staleId, out var staleViewModel)) continue;

            staleViewModel.StatusChanged -= OnPackageStatusChanged;
            staleViewModel.Dispose();
        }

        foreach (var (packageId, packageModel) in _packageService.Packages)
            try
            {
                var viewModel = GetOrCreatePackageViewModel(packageId, packageModel);

                // The root category aggregates all sub category packages, so only the concrete
                // category has to be filled here.
                var targetCategory = ResolveCategoryForPackage(packageModel.Package) ?? _allCategory;

                targetCategory.Add(viewModel);
            }
            catch (Exception e)
            {
                _logger.Error(e.Message, e);
            }

        RefreshUpdatesCategory(false);

        // Runs after the stale view models were evicted and disposed, so the banner can never hold a
        // disposed view model.
        FeaturedPackage.Target = _packageViewModels.GetValueOrDefault(FeaturedPackageId);

        // Package data changed. Rows are updated in place, membership and order stay untouched.
        SyncSelection(() => _allCategory.Resync());
    }

    /// <summary>
    ///     Reuses the view model of a package across reconstructions. Recreating it would drop the list
    ///     selection, the resolved icon and the resolved tabs of the package the user is looking at.
    /// </summary>
    private PackageViewModel GetOrCreatePackageViewModel(string packageId, IPackageState packageModel)
    {
        if (_packageViewModels.TryGetValue(packageId, out var existing))
        {
            // RefreshAsync replaces the package states, the view model has to follow.
            if (!ReferenceEquals(existing.PackageState, packageModel))
                existing.PackageState = packageModel;

            return existing;
        }

        var viewModel = new PackageViewModel(packageModel, _packageService, _httpService, _windowService,
            _applicationStateService, _logger);

        viewModel.StatusChanged += OnPackageStatusChanged;
        _packageViewModels[packageId] = viewModel;
        return viewModel;
    }

    private void OnPackageStatusChanged(object? sender, EventArgs e)
    {
        RefreshUpdateCount();
    }

    /// <summary>
    ///     Recomputes membership and order of the whole tree. Only ever called in response to a user
    ///     action, never because package data changed.
    /// </summary>
    private void Relayout()
    {
        RefreshUpdatesCategory(true);

        var query = new PackageListQuery(Filter, _showInstalled, _showAvailable);
        SyncSelection(() => _allCategory.Relayout(query));
    }

    /// <summary>
    ///     The list box drops its selection while its items are reconciled, which would blank the detail
    ///     pane. The selected package is therefore restored by id afterwards.
    /// </summary>
    private void SyncSelection(Action layout)
    {
        var selectedPackageId = SelectedCategory?.SelectedPackage?.PackageState.Package.Id;

        layout();

        // Depends on the freshly laid out content, so it has to run after the layout.
        RefreshUpdateCount();

        if (SelectedCategory is { IsVisible: false })
            SelectedCategory = _allCategory;

        if (SelectedCategory == null) return;

        SelectedCategory.SelectedPackage = selectedPackageId == null
            ? null
            : SelectedCategory.VisiblePackages.FirstOrDefault(x => x.PackageState.Package.Id == selectedPackageId);
    }

    /// <summary>
    ///     Refreshes the smart updates category. On a data change the previous content is kept, so a
    ///     package updated from within the category stays in place instead of being pulled away.
    /// </summary>
    private void RefreshUpdatesCategory(bool relayout)
    {
        var live = _packageViewModels.Values.ToHashSet();
        var updatable = live.Where(x => x.HasUpdate);

        // Carried over packages must still exist, otherwise an evicted and disposed view model would be
        // resurrected and keep rendering a row for a package the service no longer knows.
        _updatesCategory.SetPackages(relayout
            ? updatable
            : updatable.Concat(_updatesCategory.Packages.Where(live.Contains)).Distinct());
    }

    private void RefreshUpdateCount()
    {
        _updatesCategory.SetLiveCount(_packageViewModels.Values.Count(x => x.HasUpdate));
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
        // A prerelease installation stays on the prerelease channel, so its updates are offered
        // here too.
        var packages = _packageService.Packages.Values
            .Where(x => x.Status is PackageStatus.UpdateAvailable or PackageStatus.UpdateAvailablePrerelease)
            .Select(x => (State: x, Target: x.ResolveTargetVersion()))
            .Where(x => x.Target != null)
            .OrderBy(x => x.State.Package.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (packages.Count == 0)
            return true;

        var requiresRestart = packages.Any(x =>
            string.Equals(x.State.Package.Type, "Plugin", StringComparison.OrdinalIgnoreCase));

        var packageLines = packages.Select(x =>
        {
            var installedVersion = x.State.InstalledVersion?.Version ?? "?";

            return $"- **{x.State.Package.Name}** `{installedVersion} -> {x.Target!.Version}`";
        });

        var message =
            $"The following packages will be updated:\n\n{string.Join("\n", packageLines)}\n\n" +
            (requiresRestart
                ? "> Restart will be required after the update completes."
                : "Do you want to continue?");

        var confirmation = await _windowService.ShowMessageBoxAsync(new MessageBoxRequest
        {
            Title = "Update Packages",
            Message = message,
            Icon = MessageBoxIcon.Info,
            Buttons =
            [
                new MessageBoxButton
                {
                    Text = "Update All",
                    Role = MessageBoxButtonRole.Yes,
                    Style = MessageBoxButtonStyle.Primary,
                    IsDefault = true
                },
                new MessageBoxButton
                {
                    Text = "Cancel",
                    Role = MessageBoxButtonRole.Cancel,
                    Style = MessageBoxButtonStyle.Secondary
                }
            ]
        });

        if (!confirmation.IsAccepted)
            return false;
        
        foreach (var package in packages)
        {
            await FocusPluginAsync(package.State.Package!.Id!);
            await _packageService.UpdateAsync(package.State.Package.Id!, package.Target, false, true);
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

    private PackageCategoryViewModel? ResolveCategoryForPackage(Essentials.PackageManager.Package package)
    {
        var rawCategory = package.Category;
        var hasPath = !string.IsNullOrWhiteSpace(rawCategory) &&
                      rawCategory.IndexOfAny(CategorySeparators) >= 0;

        if (hasPath)
        {
            var segments = SplitCategoryPath(rawCategory);
            if (segments.Length == 0) return ResolveRootCategoryForType(package.Type);

            var current = _allCategory.GetOrCreateSubCategory(segments[0]);

            for (var i = 1; i < segments.Length; i++)
                current = current.GetOrCreateSubCategory(
                    NormalizeCategorySegment(segments[i], i == segments.Length - 1));

            return current;
        }

        var category = ResolveRootCategoryForType(package.Type);
        if (category == null) return null;

        var wantedCategory = rawCategory;
        if (string.IsNullOrWhiteSpace(wantedCategory))
            return category;

        wantedCategory = NormalizeCategorySegment(wantedCategory, true);

        return category.GetOrCreateSubCategory(wantedCategory);
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
        return FindCategory(_allCategory.SubCategories, rootCategoryName);
    }

    private static void ClearCategoryPackages(PackageCategoryViewModel category)
    {
        // Only the backing list is cleared, the visible collections are reconciled afterwards.
        if (category.Kind != PackageCategoryKind.Updates) category.ClearPackages();

        foreach (var subCategory in category.SubCategories)
            ClearCategoryPackages(subCategory);
    }
}
