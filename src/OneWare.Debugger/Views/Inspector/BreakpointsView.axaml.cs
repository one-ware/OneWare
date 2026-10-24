using Avalonia.Controls;
using OneWare.Debugger.ViewModels.Inspector;
using OneWare.Essentials.EditorExtensions;

namespace OneWare.Debugger.Views.Inspector;

public partial class BreakpointsView : UserControl
{
    public BreakpointsView()
    {
        InitializeComponent();
    }
    
    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not BreakpointsViewModel vm) return;

        vm.SelectedBreakpoints.Clear();
        foreach (var item in BreakpointGrid.SelectedItems.OfType<BreakPoint>())
            vm.SelectedBreakpoints.Add(item);
    }
    
}
