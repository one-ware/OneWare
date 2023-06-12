using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactions.Core;
using Avalonia.Xaml.Interactivity;
using System.Globalization;
using System.Reflection;
using Avalonia.Reactive;

namespace OneWare.Shared.Behaviours;

public class ActionTriggerBehaviour : Trigger
{
    public static readonly StyledProperty<Action?> TriggerActionProperty =
        AvaloniaProperty.Register<ActionTriggerBehaviour, Action?>(nameof(TriggerAction));

    /// <summary>
    /// Gets or sets the name of the event to listen for. This is a avalonia property.
    /// </summary>
    public Action? TriggerAction
    {
        get => GetValue(TriggerActionProperty);
        set => SetValue(TriggerActionProperty, value);
    }


    /// <summary>
    /// Called after the behavior is attached to the <see cref="Behavior.AssociatedObject"/>.
    /// </summary>
    protected override void OnAttached()
    {
        TriggerAction = OnInvoked;
        base.OnAttached();
    }

    /// <summary>
    /// Called when the behavior is being detached from its <see cref="Behavior.AssociatedObject"/>.
    /// </summary>
    protected override void OnDetaching()
    {
        TriggerAction = null;
        base.OnDetaching();
    }

    private void OnInvoked()
    {
        Interaction.ExecuteActions(this, Actions, EventArgs.Empty);
    }
}
