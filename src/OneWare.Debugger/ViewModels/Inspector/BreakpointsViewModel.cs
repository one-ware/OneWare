using System.Collections.ObjectModel;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.EditorExtensions;

namespace OneWare.Debugger.ViewModels.Inspector;

// "Breakpoints" tab in the debugger panel: all breakpoints set, across all
// files. Takes its data directly from the application-wide BreakpointStore,
// so it also shows breakpoints from files that are not open right now. It is the same
// store the running session passes on to GDB - the list thus shows exactly what
// is armed on the target.
public partial class BreakpointsViewModel : ObservableObject
{
    public ObservableCollection<BreakPoint> SelectedBreakpoints { get; } = new();

    public BreakpointsViewModel()
    {
        Breakpoints = new DataGridCollectionView(BreakpointStore.Instance.Breakpoints);
        Breakpoints.SortDescriptions.Add(DataGridSortDescription.FromPath(nameof(BreakPoint.File)));
        Breakpoints.SortDescriptions.Add(DataGridSortDescription.FromPath(nameof(BreakPoint.Line)));

        SelectedBreakpoints.CollectionChanged += (_, _) =>
            RemoveBreakpointCommand.NotifyCanExecuteChanged();

        // On the store and not on the view: whether there is anything to clear at all does
        // not depend on how the list is currently sorted or filtered.
        // Store and view model are both singletons and live as long as the app -
        // the subscription never has to be released.
        BreakpointStore.Instance.Breakpoints.CollectionChanged += (_, _) =>
        {
            RemoveAllBreakpointsCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HasBreakpoints));
        };

        // IsVerified changes in the object, not in the collection - without this subscription the
        // column would stay on the value it read when opened.
        BreakpointStore.Instance.VerificationChanged += (_, _) => Breakpoints.Refresh();
    }

    public bool HasBreakpoints => BreakpointStore.Instance.Breakpoints.Count > 0;

    private bool CanRemoveBreakpoint() => SelectedBreakpoints.Count > 0;

    private bool CanRemoveAllBreakpoints() => BreakpointStore.Instance.Breakpoints.Count > 0;

    // Sorted view of the store: by file, then by line. Line
    // is an int and is therefore sorted numerically - line 9 comes before line 10,
    // not after it. The sorting can be changed at runtime through the column headers;
    // the one set here applies on opening.
    public DataGridCollectionView Breakpoints { get; }

    [RelayCommand(CanExecute = nameof(CanRemoveBreakpoint))]
    private void RemoveBreakpoint()
    {
        foreach (var breakpoint in SelectedBreakpoints.ToArray()) BreakpointStore.Instance.Remove(breakpoint);
    }

    [RelayCommand(CanExecute = nameof(CanRemoveAllBreakpoints))]
    private void RemoveAllBreakpoints()
    {
        // Do not empty the store with Clear(): the session listens to CollectionChanged and needs the
        // removed entries one by one to clear them on the target too. Clear only reports a Reset.
        foreach (var breakpoint in BreakpointStore.Instance.Breakpoints.ToArray())
            BreakpointStore.Instance.Remove(breakpoint);
        // Purely local selection - Clear() is harmless here, only CanExecute depends on it.
        SelectedBreakpoints.Clear();
    }
}
