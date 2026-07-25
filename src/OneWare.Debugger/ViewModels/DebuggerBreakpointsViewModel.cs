using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.EditorExtensions;

namespace OneWare.Debugger.ViewModels;

/// <summary>
///     Reiter "Breakpoints" im Debugging-Panel: alle gesetzten Breakpoints, quer ueber alle
///     Dateien. Bezieht seine Daten direkt aus dem anwendungsweiten <see cref="BreakpointStore" />,
///     zeigt also auch Breakpoints aus Dateien, die gerade nicht geoeffnet sind.
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
        BreakpointStore.Instance.Breakpoints.Clear();
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
