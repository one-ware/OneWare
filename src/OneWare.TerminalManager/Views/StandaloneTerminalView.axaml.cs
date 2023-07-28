using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OneWare.TerminalManager.Views;

public partial class StandaloneTerminalView : UserControl
{
    public StandaloneTerminalView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}