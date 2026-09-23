using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Enums;

namespace OneWare.PackageManager.ViewModels;

public class PackageCategoryViewModel : ObservableObject
{
    /// <summary>
    ///     Label for packages sitting directly in a category that also has sub categories.
    /// </summary>
    private const string OwnGroupHeader = "Other";

    private const string OwnGroupKey = "own";

    private sealed record PackageGroup(string? Key, string? Header, IReadOnlyList<PackageViewModel> Packages);

    private readonly List<PackageViewModel> _packages = [];

    /// <summary>
    ///     Ids that were visible at the last user triggered relayout. They stay visible across data
    ///     changes so that installing a package does not make its row disappear or move.
    /// </summary>
    private readonly HashSet<string> _pinnedIds = [];

    private readonly Dictionary<string, PackageSeparatorViewModel> _separatorCache = new();

    private int _displayCount;
    private bool _isExpanded = true;
    private bool _isVisible = true;
    private PackageListQuery _query = PackageListQuery.Default;
    private PackageViewModel? _selectedPackage;

    /// <summary>
    ///     The expansion state the user chose, restored once a search is cleared again.
    /// </summary>
    private bool _userExpanded = true;

    private bool _suppressExpansionTracking;

    public PackageCategoryViewModel(string header, PackageCategoryKind kind = PackageCategoryKind.Normal)
    {
        Header = header;
        Kind = kind;
    }

    public string Header { get; }

    public PackageCategoryKind Kind { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (!SetProperty(ref _isExpanded, value)) return;
            if (!_suppressExpansionTracking) _userExpanded = value;
        }
    }

    /// <summary>
    ///     Whether the category is shown in the category tree.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    /// <summary>
    ///     Count badge shown in the tree. For the updates node this is a live count, so it can change
    ///     without relayouting anything.
    /// </summary>
    public int DisplayCount
    {
        get => _displayCount;
        private set => SetProperty(ref _displayCount, value);
    }

    public PackageViewModel? SelectedPackage
    {
        get => _selectedPackage;
        set => SetProperty(ref _selectedPackage, value);
    }

    public IReadOnlyList<PackageViewModel> Packages => _packages;

    public ObservableCollection<PackageViewModel> VisiblePackages { get; } = [];

    public ObservableCollection<PackageListEntryViewModel> VisibleEntries { get; } = [];

    public ObservableCollection<PackageCategoryViewModel> SubCategories { get; } = [];

    public PackageCategoryViewModel GetOrCreateSubCategory(string header,
        PackageCategoryKind kind = PackageCategoryKind.Normal)
    {
        var existing = SubCategories.FirstOrDefault(x =>
            x.Kind == kind && x.Header.Equals(header, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;

        var category = new PackageCategoryViewModel(header, kind);
        SubCategories.Add(category);
        return category;
    }

    public void Add(PackageViewModel model)
    {
        _packages.Add(model);
    }

    public void ClearPackages()
    {
        _packages.Clear();
    }

    public void SetPackages(IEnumerable<PackageViewModel> packages)
    {
        // Materialise first, the caller may pass a lazy sequence reading from this very list.
        var materialized = packages.ToList();

        _packages.Clear();
        _packages.AddRange(materialized);
    }

    /// <summary>
    ///     Recomputes membership and order for a new query. This is the only place where rows are
    ///     allowed to move, and it only runs in response to a user action.
    /// </summary>
    public void Relayout(PackageListQuery query)
    {
        Apply(query, honorPins: false);
    }

    /// <summary>
    ///     Reapplies the current query after a data change. Rows that were visible before stay visible
    ///     in place, so installing or updating a package never disturbs the list.
    /// </summary>
    public void Resync()
    {
        Apply(_query, honorPins: true);
    }

    private void Apply(PackageListQuery query, bool honorPins)
    {
        // A category whose header matches the search shows all of its packages, so searching works for
        // categories and packages alike.
        var headerMatches = Kind == PackageCategoryKind.Normal && MatchesFilter(Header, query.Filter);
        var effective = headerMatches ? query with { Filter = string.Empty } : query;

        _query = effective;

        foreach (var subCategory in SubCategories)
            subCategory.Apply(effective, honorPins);

        Rebuild(headerMatches, honorPins);
    }

    private void Rebuild(bool headerMatches, bool honorPins)
    {
        var own = _packages.Where(x => IsEligible(x, honorPins)).Distinct().ToList();
        own.Sort(new PackageListComparer(_query.Filter));

        // The updates node is a view onto packages that already live in a real category, it must not
        // contribute to the aggregate of its parent.
        var subGroups = SubCategories
            .Where(x => x.Kind != PackageCategoryKind.Updates && x.VisiblePackages.Count > 0)
            .Select(x => (Header: x.Header, Packages: (IReadOnlyList<PackageViewModel>)x.VisiblePackages.ToList()))
            .ToList();

        var groups = new List<PackageGroup>();

        if (subGroups.Count == 0)
        {
            if (own.Count > 0) groups.Add(new PackageGroup(null, null, own));
        }
        else
        {
            // The own group and a sub category could carry the same label, so separators are cached by a
            // key that keeps their instances distinct.
            if (own.Count > 0) groups.Add(new PackageGroup(OwnGroupKey, OwnGroupHeader, own));

            foreach (var subGroup in subGroups)
                groups.Add(new PackageGroup("sub:" + subGroup.Header, subGroup.Header, subGroup.Packages));
        }

        ObservableCollectionReconciler.Reconcile(VisiblePackages, groups.SelectMany(x => x.Packages).ToList());
        ObservableCollectionReconciler.Reconcile(VisibleEntries, BuildEntries(groups));

        if (!honorPins)
        {
            _pinnedIds.Clear();
            foreach (var package in VisiblePackages)
                _pinnedIds.Add(package.PackageState.Package.Id ?? string.Empty);
        }

        if (Kind != PackageCategoryKind.Updates)
        {
            DisplayCount = VisiblePackages.Count;
            IsVisible = Kind == PackageCategoryKind.Root || headerMatches || VisiblePackages.Count > 0;
        }

        _suppressExpansionTracking = true;
        // While searching everything is expanded so matches inside collapsed branches stay reachable.
        IsExpanded = _query.HasSearch || _userExpanded;
        _suppressExpansionTracking = false;
    }

    /// <summary>
    ///     Sets the live update count for the updates node, which changes as packages are installed
    ///     without triggering any relayout.
    /// </summary>
    public void SetLiveCount(int count)
    {
        DisplayCount = count;
        IsVisible = count > 0 || VisiblePackages.Count > 0;
    }

    private bool IsEligible(PackageViewModel package, bool honorPins)
    {
        if (honorPins && _pinnedIds.Contains(package.PackageState.Package.Id ?? string.Empty))
            return true;

        if (!MatchesFilter(package.PackageState.Package.Name, _query.Filter)) return false;

        var installed = IsInstalledPackage(package.PackageState.Status);
        if (!_query.ShowInstalled && installed) return false;
        if (!_query.ShowAvailable && !installed) return false;

        return true;
    }

    private static bool MatchesFilter(string? value, string filter)
    {
        if (string.IsNullOrEmpty(filter)) return true;
        return value?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private static bool IsInstalledPackage(PackageStatus status)
    {
        return status is PackageStatus.Installed
            or PackageStatus.UpdateAvailable
            or PackageStatus.UpdateAvailablePrerelease
            or PackageStatus.NeedRestart
            or PackageStatus.Installing;
    }

    private IReadOnlyList<PackageListEntryViewModel> BuildEntries(IReadOnlyList<PackageGroup> groups)
    {
        if (groups.Count == 0) return [];

        if (groups.Count == 1 && groups[0].Key == null)
            return groups[0].Packages.Cast<PackageListEntryViewModel>().ToList();

        var entries = new List<PackageListEntryViewModel>();

        foreach (var group in groups)
        {
            entries.Add(GetSeparator(group.Key!, group.Header!));
            entries.AddRange(group.Packages);
        }

        return entries;
    }

    private PackageSeparatorViewModel GetSeparator(string key, string label)
    {
        if (!_separatorCache.TryGetValue(key, out var separator))
        {
            separator = new PackageSeparatorViewModel(label);
            _separatorCache[key] = separator;
        }

        return separator;
    }
}
