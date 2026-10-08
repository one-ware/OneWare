using Avalonia;
using Avalonia.Controls;
using OneWare.SourceControl.ViewModels;

namespace OneWare.SourceControl.Views;

public partial class SourceControlMainWindowBottomRightExtension : UserControl
{
    public SourceControlMainWindowBottomRightExtension()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        (DataContext as SourceControlViewModel)?.AttachTopLevel(TopLevel.GetTopLevel(this));
    }
}
