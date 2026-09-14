using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Services;

namespace OneWare.PackageManager.ViewModels;

public partial class PackageManagerViewModel
{
    private readonly Dictionary<string, PackageViewModel> _cache = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    public ObservableCollection<PackageViewModel> BrowsePackages { get; } = [];
    public ObservableCollection<PackageViewModel> FeaturedPackages { get; } = [];
    public ObservableCollection<string> CategoryFilters { get; } = ["All"];
    public string CategoryFilter
    {
        get;
        set { if (SetProperty(ref field, value)) FilterPackages(); }
    } = "All";

    public PackageViewModel? SelectedPackage
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;
            OnPropertyChanged(nameof(IsDetails));
            OnPropertyChanged(nameof(ShowFeatured));
            if (value != null) { _ = value.ResolveTabsAsync(); _ = value.ResolveIconAsync(); }
        }
    }
    public bool IsDetails => SelectedPackage != null;
    public bool ShowFeatured => !IsDetails && SelectedFilterIndex == 0 && string.IsNullOrWhiteSpace(Filter) && CategoryFilter == "All" && FeaturedPackages.Count > 0;
    public bool IsUpdates => SelectedFilterIndex == 2;
    public int UpdateCount => _cache.Values.Count(x => x.PackageState.Status is PackageStatus.UpdateAvailable or PackageStatus.UpdateAvailablePrerelease);
    public string UpdatesLabel => $"Updates ({UpdateCount})";
    public string ResultsLabel => $"{BrowsePackages.Count} packages";
    public bool ShowEmptyState => !IsLoading && BrowsePackages.Count == 0;
    public string? SourceWarning { get; private set => SetProperty(ref field, value); }
    public bool RestartRequired => _cache.Values.Any(x => x.PackageState.Status == PackageStatus.NeedRestart);
    public RelayCommand BackCommand => field ??= new(() => SelectedPackage = null);
    public RelayCommand ClearSearchCommand => field ??= new(() => { Filter = ""; FilterPackages(); });
    public RelayCommand ResetFiltersCommand => field ??= new(() => { Filter = ""; CategoryFilter = "All"; FilterPackages(); });
    public AsyncRelayCommand<string> OpenDependencyCommand => field ??= new(async id => { if (id != null) await FocusPluginAsync(id); });

    private void OnPackageChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName is "Status" or "InstalledVersion" or "Package")
            Dispatcher.UIThread.Post(() =>
            {
                if (_disposed) return;
                if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == "Package") ConstructPackageViewModels();
                else FilterPackages();
                foreach (var vm in _cache.Values) vm.RefreshDependencies();
            });
    }

    private void RefreshCategoryFilters()
    {
        static IEnumerable<string> Paths(PackageCategoryViewModel category, string? parent = null)
        {
            var path = parent == null ? category.Header : $"{parent}/{category.Header}";
            yield return path;
            foreach (var child in category.SubCategories)
                foreach (var nested in Paths(child, path)) yield return nested;
        }
        var categories = PackageCategories.SelectMany(x => Paths(x))
            .Concat(_cache.Values.Select(x => x.PackageState.Package.Category).OfType<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).ToArray();
        foreach (var category in categories) if (!CategoryFilters.Contains(category)) CategoryFilters.Add(category);
    }

    private bool MatchesCategory(PackageViewModel vm)
    {
        var package = vm.PackageState.Package;
        var raw = (package.Category ?? "").Replace('\\', '/');
        var root = ResolveRootCategoryForType(package.Type)?.Header;
        var path = raw.Contains('/') ? raw : $"{root}/{NormalizeCategorySegment(raw, true)}";
        return CategoryFilter == "All" || CategoryFilter == package.Type || CategoryFilter == raw || CategoryFilter == root ||
            path.Equals(CategoryFilter, StringComparison.OrdinalIgnoreCase) || path.StartsWith(CategoryFilter + "/", StringComparison.OrdinalIgnoreCase);
    }

    // Preserve realized rows, selection and scroll when status notifications do not change membership.
    private static void Reconcile(ObservableCollection<PackageViewModel> collection, IReadOnlyList<PackageViewModel> desired)
    {
        for (var i = collection.Count - 1; i >= 0; i--)
            if (!desired.Contains(collection[i])) collection.RemoveAt(i);
        for (var i = 0; i < desired.Count; i++)
        {
            var index = collection.IndexOf(desired[i]);
            if (index < 0) collection.Insert(i, desired[i]);
            else if (index != i) collection.Move(index, i);
        }
    }

    private void RefreshBrowse()
    {
        RefreshCategoryFilters();
        var desired = _cache.Values.Select(vm => (Vm: vm, Score: PackageSearch.Score(vm.PackageState.Package, Filter)))
                     .Where(x => x.Score != null)
                     .Where(x => SelectedFilterIndex switch { 1 => x.Vm.PackageState.InstalledVersion != null,
                         2 => x.Vm.PackageState.Status is PackageStatus.UpdateAvailable or PackageStatus.UpdateAvailablePrerelease, _ => true })
                     .Where(x => MatchesCategory(x.Vm))
                     .OrderBy(x => x.Score).ThenBy(x => x.Vm.PackageState.Package.Name, StringComparer.OrdinalIgnoreCase)
                     .Select(x => x.Vm).ToArray();
        Reconcile(BrowsePackages, desired);
        Reconcile(FeaturedPackages, ((_packageService as IPackageDiscoveryService)?.FeaturedPackageIds ?? [])
            .Distinct().Where(_cache.ContainsKey).Take(3).Select(id => _cache[id]).ToArray());
        OnPropertyChanged(nameof(ShowFeatured));
        OnPropertyChanged(nameof(IsUpdates));
        OnPropertyChanged(nameof(UpdateCount));
        OnPropertyChanged(nameof(UpdatesLabel));
        OnPropertyChanged(nameof(ResultsLabel));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(RestartRequired));
        UpdateAllCommand?.NotifyCanExecuteChanged();
    }
}