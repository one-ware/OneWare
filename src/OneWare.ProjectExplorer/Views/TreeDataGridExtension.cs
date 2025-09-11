using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace OneWare.ProjectExplorer.Views;

public sealed class TreeDataGridExtension : AvaloniaObject
{
    static TreeDataGridExtension()
    {
        IsExpandedExtensionProperty.Changed.AddClassHandler<TreeDataGridExpanderCell>(HandleIsExpandedChanged);
    }
    
    public static readonly AttachedProperty<bool> IsExpandedExtensionProperty = AvaloniaProperty.RegisterAttached<TreeDataGridExtension, TreeDataGridExpanderCell, bool>(
        "IsExpandedExtension", false, false, BindingMode.OneTime);

    private static void HandleIsExpandedChanged(TreeDataGridExpanderCell element, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.GetNewValue<bool>())
            element.IsExpanded = true;
    }
}