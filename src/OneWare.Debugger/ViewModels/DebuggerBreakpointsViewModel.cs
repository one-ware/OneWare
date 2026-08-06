using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.EditorExtensions;

namespace OneWare.Debugger.ViewModels;

/// <summary>
///     Reiter "Breakpoints" im Debugger-Panel: alle gesetzten Breakpoints, quer ueber alle
///     Dateien. Bezieht seine Daten direkt aus dem anwendungsweiten <see cref="BreakpointStore" />,
///     zeigt also auch Breakpoints aus Dateien, die gerade nicht geoeffnet sind. Es ist derselbe
///     Store, den die laufende Session an GDB weiterreicht - die Liste zeigt damit genau das, was
///     am Ziel scharf ist.
/// </summary>
public partial class DebuggerBreakpointsViewModel : ObservableObject
{
    public ObservableCollection<BreakPoint> SelectedBreakpoints { get; } = new();
    
    public DebuggerBreakpointsViewModel()
    {
        SelectedBreakpoints.CollectionChanged += (_, _) =>
            RemoveBreakpointCommand.NotifyCanExecuteChanged();

        // Store und ViewModel sind beide Singletons und leben so lange wie die App -
        // das Abo muss nicht wieder geloest werden.
        Breakpoints.CollectionChanged += (_, _) =>
            RemoveAllBreakpointsCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveBreakpoint() => SelectedBreakpoints.Count > 0;

    private bool CanRemoveAllBreakpoints() => Breakpoints.Count > 0;

    public ObservableCollection<BreakPoint> Breakpoints => BreakpointStore.Instance.Breakpoints;

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
        foreach (var breakpoint in Breakpoints.ToArray()) BreakpointStore.Instance.Remove(breakpoint);
        // Rein lokale Auswahl - hier ist Clear() unkritisch, daran haengt nur CanExecute.
        SelectedBreakpoints.Clear();
    }
}
