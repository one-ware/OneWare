using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactions.Events;
using Avalonia.Xaml.Interactivity;

namespace OneWare.Essentials.Behaviors;

public class PointerPressedHandledEventTrigger : InteractiveTriggerBase
{
    static PointerPressedHandledEventTrigger()
    {
        RoutingStrategiesProperty.OverrideMetadata<PointerPressedEventTrigger>(
            new StyledPropertyMetadata<RoutingStrategies>(
                defaultValue: RoutingStrategies.Tunnel | RoutingStrategies.Bubble));
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree()
    {
        AssociatedObject?.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies, true);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree()
    {
        AssociatedObject?.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Execute(e);
    }
}