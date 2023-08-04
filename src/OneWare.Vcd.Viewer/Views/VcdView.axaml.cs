using Avalonia.Controls;
using Avalonia.Input;
using OneWare.Vcd.Viewer.ViewModels;

namespace OneWare.Vcd.Viewer.Views;

public partial class VcdView : UserControl
{
    public VcdView()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control) _ = (DataContext as VcdViewModel)?.SaveAsync();
        base.OnKeyDown(e);
    }
}