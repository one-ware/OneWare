using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Xaml.Interactions.DragAndDrop;

namespace OneWare.Essentials.Behaviors;

public class ContextDragBubbleBehavior : ContextDragBehavior
{
    /// <inheritdoc />
    protected override void OnAttachedToVisualTree()
    {
        AssociatedObject?.AddHandler(InputElement.PointerPressedEvent, AssociatedObject_PointerPressed, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        base.OnAttachedToVisualTree();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree()
    {
        base.OnDetachedFromVisualTree();
        AssociatedObject?.RemoveHandler(InputElement.PointerPressedEvent, AssociatedObject_PointerPressed);
    }

    private void AssociatedObject_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(AssociatedObject).Properties;
        
        if (properties.IsLeftButtonPressed && IsEnabled)
        {
            if (e.Source is Control control
                && AssociatedObject?.DataContext == control.DataContext)
            {
                if(control.FindLogicalAncestorOfType<CheckBox>() != null)
                    e.Handled = false;
                else if(control.FindLogicalAncestorOfType<Button>() != null)
                    e.Handled = false;
            }
        }
    }
}