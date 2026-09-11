using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Controls;

public partial class DeviceCodeLoginPanel : UserControl
{
    public DeviceCodeLoginPanel()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // FlexibleWindowViewModelBase.OnWindowOpened is not raised reliably for every host,
        // so the login is (also) kicked off here. Starting is idempotent.
        if (DataContext is DeviceCodeLoginViewModel vm && this.FindAncestorOfType<FlexibleWindow>() is { } window)
            vm.TryStartLogin(window);
    }
}
