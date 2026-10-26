using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.Helpers;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Services;

namespace OneWare.Debugger.ViewModels.Inspector;

// "Breakpoints" tab in the debugger panel: the breakpoints of the active project, across all
// its files. Takes its data from the application-wide BreakpointStore, so it also shows
// breakpoints from files that are not open right now. Breakpoints of other projects stay in
// the store (and in their editors) but are not listed - the debugger only works on the active project.
public partial class BreakpointsViewModel : ObservableObject
{
    private readonly IProjectExplorerService _projectExplorerService;

    // Mirror of the store limited to the active project and the filter. Kept as an own collection instead of a
    // DataGridCollectionView filter: removing a filtered-out item from the source crashes the view.
    private readonly ObservableCollection<BreakPoint> _projectBreakpoints = [];

    public ObservableCollection<BreakPoint> SelectedBreakpoints { get; } = new();

    public BreakpointsViewModel(IProjectExplorerService projectExplorerService)
    {
        _projectExplorerService = projectExplorerService;

        Breakpoints = new DataGridCollectionView(_projectBreakpoints);
        Breakpoints.SortDescriptions.Add(DataGridSortDescription.FromPath(nameof(BreakPoint.File)));
        Breakpoints.SortDescriptions.Add(DataGridSortDescription.FromPath(nameof(BreakPoint.Line)));

        SelectedBreakpoints.CollectionChanged += (_, _) =>
            RemoveBreakpointCommand.NotifyCanExecuteChanged();

        // Store, explorer and view model are singletons and live as long as the app -
        // the subscriptions never have to be released.
        BreakpointStore.Instance.Breakpoints.CollectionChanged += OnStoreChanged;
        _projectExplorerService.PropertyChanged += OnProjectExplorerPropertyChanged;

        // IsVerified changes in the object, not in the collection - without this subscription the
        // column would stay on the value it read when opened.
        BreakpointStore.Instance.VerificationChanged += (_, _) => Breakpoints.Refresh();

        Rebuild();
    }

    // Filter typed into the search field: matches the project-relative path or the line ("top.vhd:12" works too).
    [ObservableProperty] private string _filterText = string.Empty;

    // Whether the active project has any breakpoint at all - independent of the filter.
    public bool HasBreakpoints => BreakpointStore.Instance.Breakpoints.Any(IsInActiveProject);

    // The project has breakpoints, but the filter hides all of them.
    public bool HasNoMatches => HasBreakpoints && _projectBreakpoints.Count == 0;

    // Sorted view of the project's breakpoints: by file, then by line. Line
    // is an int and is therefore sorted numerically - line 9 comes before line 10,
    // not after it. The sorting can be changed at runtime through the column headers;
    // the one set here applies on opening.
    public DataGridCollectionView Breakpoints { get; }

    private bool CanRemoveBreakpoint() => SelectedBreakpoints.Count > 0;

    private bool CanRemoveAllBreakpoints() => _projectBreakpoints.Count > 0;

    [RelayCommand(CanExecute = nameof(CanRemoveBreakpoint))]
    private void RemoveBreakpoint()
    {
        foreach (var breakpoint in SelectedBreakpoints.ToArray()) BreakpointStore.Instance.Remove(breakpoint);
    }

    // Removes the listed breakpoints only - those of other projects or hidden by the filter are not on display.
    [RelayCommand(CanExecute = nameof(CanRemoveAllBreakpoints))]
    private void RemoveAllBreakpoints()
    {
        // Do not empty the store with Clear(): the session listens to CollectionChanged and needs the
        // removed entries one by one to clear them on the target too. Clear only reports a Reset.
        foreach (var breakpoint in _projectBreakpoints.ToArray())
            BreakpointStore.Instance.Remove(breakpoint);
        // Purely local selection - Clear() is harmless here, only CanExecute depends on it.
        SelectedBreakpoints.Clear();
    }

    private bool IsInActiveProject(BreakPoint breakpoint) =>
        ProjectScope.Contains(_projectExplorerService.ActiveProject?.RootFolderPath, breakpoint.File);

    private bool IsListed(BreakPoint breakpoint) => IsInActiveProject(breakpoint) && MatchesFilter(breakpoint);

    private bool MatchesFilter(BreakPoint breakpoint)
    {
        var filter = FilterText.Trim();
        if (filter.Length == 0) return true;

        var root = _projectExplorerService.ActiveProject?.RootFolderPath;
        var path = root == null ? breakpoint.File : Path.GetRelativePath(root, breakpoint.File).Replace('\\', '/');
        var text = $"{path}:{breakpoint.Line}";

        return text.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnFilterTextChanged(string value) => Rebuild();

    private void OnProjectExplorerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IProjectExplorerService.ActiveProject)) Rebuild();
    }

    private void OnStoreChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            Rebuild();
            return;
        }

        if (e.OldItems != null)
            foreach (BreakPoint breakpoint in e.OldItems)
            {
                _projectBreakpoints.Remove(breakpoint);
                SelectedBreakpoints.Remove(breakpoint);
            }

        if (e.NewItems != null)
            foreach (BreakPoint breakpoint in e.NewItems)
                if (IsListed(breakpoint) && !_projectBreakpoints.Contains(breakpoint))
                    _projectBreakpoints.Add(breakpoint);

        OnProjectBreakpointsChanged();
    }

    private void Rebuild()
    {
        SelectedBreakpoints.Clear();
        _projectBreakpoints.Clear();
        foreach (var breakpoint in BreakpointStore.Instance.Breakpoints.Where(IsListed))
            _projectBreakpoints.Add(breakpoint);

        OnProjectBreakpointsChanged();
    }

    private void OnProjectBreakpointsChanged()
    {
        RemoveAllBreakpointsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasBreakpoints));
        OnPropertyChanged(nameof(HasNoMatches));
    }
}
