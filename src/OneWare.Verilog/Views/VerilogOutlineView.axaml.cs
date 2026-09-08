using Avalonia.Controls;
using Avalonia.Input;
using OneWare.Verilog.ViewModels;

namespace OneWare.Verilog.Views;

public partial class VerilogOutlineView : UserControl
{
    public VerilogOutlineView()
    {
        InitializeComponent();
    }

    private void TreeView_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is VerilogOutlineViewModel viewModel)
            _ = viewModel.NavigateAsync();
    }
}
