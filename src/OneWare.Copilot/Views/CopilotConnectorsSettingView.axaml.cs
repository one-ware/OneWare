using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using OneWare.Copilot.ViewModels;

namespace OneWare.Copilot.Views;

public partial class CopilotConnectorsSettingView : UserControl
{
    public CopilotConnectorsSettingView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        (DataContext as CopilotConnectorsSettingViewModel)?.EnsureLoaded();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (this.IsAttachedToVisualTree()) (DataContext as CopilotConnectorsSettingViewModel)?.EnsureLoaded();
    }
}
