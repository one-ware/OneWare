using Avalonia.Controls;
using Avalonia.Input;
using OneWare.Verilog.ViewModels;

namespace OneWare.Verilog.Views;

public partial class VerilogRtlTreeView : UserControl
{
    public VerilogRtlTreeView()
    {
        InitializeComponent();
    }

    private void TreeView_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is VerilogRtlTreeViewModel viewModel)
            _ = viewModel.NavigateAsync();
    }
}
