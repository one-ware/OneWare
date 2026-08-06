using Avalonia.Controls;
using OneWare.Debugger.ViewModels;
using OneWare.Essentials.EditorExtensions;

namespace OneWare.Debugger.Views;

public partial class DebuggerBreakpointsView : UserControl
{
    public DebuggerBreakpointsView()
    {
        InitializeComponent();
    }
    
    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not DebuggerBreakpointsViewModel vm) return;

        vm.SelectedBreakpoints.Clear();
        foreach (var item in BreakpointGrid.SelectedItems.OfType<BreakPoint>())
            vm.SelectedBreakpoints.Add(item);
    }
    
}
