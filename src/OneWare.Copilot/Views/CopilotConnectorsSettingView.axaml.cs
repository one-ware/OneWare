using Avalonia;
using Avalonia.Controls;
using OneWare.Copilot.ViewModels;

namespace OneWare.Copilot.Views;

public partial class CopilotConnectorsSettingView : UserControl
{
    private CopilotConnectorsSettingViewModel? _attached;

    public CopilotConnectorsSettingView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached?.Detach();
        _attached = null;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (VisualRoot == null) return;
        _attached?.Detach();
        _attached = null;
        Attach();
    }

    private void Attach()
    {
        _attached = DataContext as CopilotConnectorsSettingViewModel;
        _attached?.Attach();
    }
}
