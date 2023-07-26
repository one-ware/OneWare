using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using OneWare.Core.Data;
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
        if (e.Key == Key.S && e.KeyModifiers == Global.ControlKey) _ = (DataContext as VcdViewModel)?.SaveAsync();
        base.OnKeyDown(e);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}