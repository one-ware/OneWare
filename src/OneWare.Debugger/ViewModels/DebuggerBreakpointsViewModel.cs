using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.EditorExtensions;

namespace OneWare.Debugger.ViewModels;

/// <summary>
///     Reiter "Breakpoints" im Debugging-Panel: alle gesetzten Breakpoints, quer ueber alle
///     Dateien. Bezieht seine Daten direkt aus dem anwendungsweiten <see cref="BreakpointStore" />,
///     zeigt also auch Breakpoints aus Dateien, die gerade nicht geoeffnet sind. Es ist derselbe
///     Store, den die laufende Session an GDB weiterreicht - die Liste zeigt damit genau das, was
///     am Ziel scharf ist.
/// </summary>
public partial class DebuggerBreakpointsViewModel : ObservableObject
{
    [ObservableProperty] private BreakPoint? _selectedBreakpoint;

    public ObservableCollection<BreakPoint> Breakpoints => BreakpointStore.Instance.Breakpoints;

    [RelayCommand(CanExecute = nameof(CanRemoveBreakpoint))]
    private void RemoveBreakpoint()
    {
        if (SelectedBreakpoint is null) return;
        BreakpointStore.Instance.Remove(SelectedBreakpoint);
        SelectedBreakpoint = null;
    }

    [RelayCommand]
    private void RemoveAllBreakpoints()
    {
        // Nicht Clear(): die Session haengt an CollectionChanged und braucht die entfernten
        // Eintraege, um sie auch am Ziel abzuraeumen. Clear meldet sie nicht einzeln.
        foreach (var breakpoint in Breakpoints.ToArray()) BreakpointStore.Instance.Remove(breakpoint);

        SelectedBreakpoint = null;
    }

    private bool CanRemoveBreakpoint()
    {
        return SelectedBreakpoint is not null;
    }

    partial void OnSelectedBreakpointChanged(BreakPoint? value)
    {
        RemoveBreakpointCommand.NotifyCanExecuteChanged();
    }
}
