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
        //the changed event gets triggered, even when no change occur
        //this behavior only occurs when DataContext is null
        if (element.DataContext == null) return;
        
        bool value = args.GetNewValue<bool>();
        if (value != element.IsExpanded)
            element.IsExpanded = value;
    }
}