using Avalonia;
using Avalonia.Controls.Primitives;

namespace OneWare.ProjectExplorer.Views;

public abstract class TreeDataGridExtension : AvaloniaObject
{
    public static readonly AttachedProperty<bool> IsExpandedExtensionProperty =
        AvaloniaProperty.RegisterAttached<TreeDataGridExtension, TreeDataGridExpanderCell, bool>(
            "IsExpandedExtension");

    static TreeDataGridExtension()
    {
        IsExpandedExtensionProperty.Changed.AddClassHandler<TreeDataGridExpanderCell>(HandleIsExpandedChanged);
    }

    private static void HandleIsExpandedChanged(TreeDataGridExpanderCell element, AvaloniaPropertyChangedEventArgs args)
    {
        //the changed event gets triggered, even when no change occur
        //this behavior only occurs when DataContext is null
        if (element.DataContext == null) return;

        var value = args.GetNewValue<bool>();
        if (value != element.IsExpanded)
            element.IsExpanded = value;
    }
}