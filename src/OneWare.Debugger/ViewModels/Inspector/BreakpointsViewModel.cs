using System.Collections.ObjectModel;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.EditorExtensions;

namespace OneWare.Debugger.ViewModels.Inspector;

// Reiter "Breakpoints" im Debugger-Panel: alle gesetzten Breakpoints, quer ueber alle
// Dateien. Bezieht seine Daten direkt aus dem anwendungsweiten BreakpointStore,
// zeigt also auch Breakpoints aus Dateien, die gerade nicht geoeffnet sind. Es ist derselbe
// Store, den die laufende Session an GDB weiterreicht - die Liste zeigt damit genau das, was
// am Ziel scharf ist.
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

        // Am Store und nicht an der Ansicht: ob es ueberhaupt etwas abzuraeumen gibt, haengt
        // nicht daran, wie die Liste gerade sortiert oder gefiltert ist.
        // Store und ViewModel sind beide Singletons und leben so lange wie die App -
        // das Abo muss nicht wieder geloest werden.
        BreakpointStore.Instance.Breakpoints.CollectionChanged += (_, _) =>
            RemoveAllBreakpointsCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveBreakpoint() => SelectedBreakpoints.Count > 0;

    private bool CanRemoveAllBreakpoints() => BreakpointStore.Instance.Breakpoints.Count > 0;

    // Sortierte Sicht auf den Store: nach Datei, darin nach Zeile. Line
    // ist ein int und wird darum numerisch sortiert - Zeile 9 steht vor Zeile 10,
    // nicht dahinter. Ueber die Spaltenkoepfe laesst sich die Sortierung zur Laufzeit umstellen;
    // die hier gesetzte gilt beim Oeffnen.
    public DataGridCollectionView Breakpoints { get; }

    [RelayCommand(CanExecute = nameof(CanRemoveBreakpoint))]
    private void RemoveBreakpoint()
    {
        foreach (var breakpoint in SelectedBreakpoints.ToArray()) BreakpointStore.Instance.Remove(breakpoint);
    }

    [RelayCommand(CanExecute = nameof(CanRemoveAllBreakpoints))]
    private void RemoveAllBreakpoints()
    {
        // Store nicht per Clear() leeren: die Session haengt an CollectionChanged und braucht die
        // entfernten Eintraege einzeln, um sie auch am Ziel abzuraeumen. Clear meldet nur ein Reset.
        foreach (var breakpoint in BreakpointStore.Instance.Breakpoints.ToArray())
            BreakpointStore.Instance.Remove(breakpoint);
        // Rein lokale Auswahl - hier ist Clear() unkritisch, daran haengt nur CanExecute.
        SelectedBreakpoints.Clear();
    }
}
