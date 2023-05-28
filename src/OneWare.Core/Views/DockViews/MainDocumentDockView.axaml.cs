using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OneWare.Core.Views.DockViews;

public partial class MainDocumentDockView : UserControl
{
    public MainDocumentDockView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}