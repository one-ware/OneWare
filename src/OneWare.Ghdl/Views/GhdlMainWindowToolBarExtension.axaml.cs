using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OneWare.ProjectExplorer.Views;

public partial class GhdlMainWindowToolBarExtension : UserControl
{
    public GhdlMainWindowToolBarExtension()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}