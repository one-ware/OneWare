using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OneWare.ProjectExplorer.Views;

public partial class ProjectExplorerMainWindowToolBarExtension : UserControl
{
    public ProjectExplorerMainWindowToolBarExtension()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}