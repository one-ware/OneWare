using Avalonia.Controls;
using OneWare.Debugger.Models;
using OneWare.Debugger.ViewModels.Main;

namespace OneWare.Debugger.Views.Main;

public partial class MemoryTabView : UserControl
{
    public MemoryTabView()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MemoryTabViewModel vm) return;

        vm.SelectedRows.Clear();
        foreach (var row in WatchGrid.SelectedItems.OfType<MemoryRow>())
            vm.SelectedRows.Add(row);
    }
}
