using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace OneWare.ProjectExplorer.Views;

public abstract class TreeDataGridExtension : AvaloniaObject
{
    static TreeDataGridExtension()
    {
        IsExpandedExtensionProperty.Changed.AddClassHandler<TreeDataGridExpanderCell>(HandleIsExpandedChanged);
    }
    
    public static readonly AttachedProperty<bool> IsExpandedExtensionProperty = AvaloniaProperty.RegisterAttached<TreeDataGridExtension, TreeDataGridExpanderCell, bool>(
        "IsExpandedExtension", false, false, BindingMode.OneWay);

    private static void HandleIsExpandedChanged(TreeDataGridExpanderCell element, AvaloniaPropertyChangedEventArgs args)
    {
        bool value = args.GetNewValue<bool>();
        if (value && value != element.IsExpanded)
            element.IsExpanded = value;
    }
}