using Avalonia;
using Avalonia.Controls;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.UniversalFpgaProjectSystem.Views;

public partial class UniversalFpgaProjectTestBenchToolBarView : UserControl
{
    public UniversalFpgaProjectTestBenchToolBarView()
    {
        InitializeComponent();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        (DataContext as UniversalFpgaProjectTestBenchToolBarViewModel)?.Detach();
        base.OnDetachedFromVisualTree(e);
    }
}