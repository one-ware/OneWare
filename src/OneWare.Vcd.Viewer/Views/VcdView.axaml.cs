using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OneWare.Vcd.Viewer.Views;

public partial class VcdView : UserControl
{
    public VcdView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}