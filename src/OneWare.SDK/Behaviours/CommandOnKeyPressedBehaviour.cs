using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OneWare.SDK.Behaviours;

public class CommandOnKeyPressedBehaviour : CommandBasedBehaviour
{
    public static readonly StyledProperty<Key?> TriggerKeyProperty =
        AvaloniaProperty.Register<ActionTriggerBehaviour, Key?>(nameof(TriggerKey));
    
    public Key? TriggerKey
    {
        get => GetValue(TriggerKeyProperty);
        set => SetValue(TriggerKeyProperty, value);
    }
    
    private CompositeDisposable? Disposables { get; set; }

    protected override void OnAttached()
    {
        if (AssociatedObject == null) return;
        Disposables = new CompositeDisposable();

        base.OnAttached();

        Disposables.Add(AssociatedObject.AddDisposableHandler(
            InputElement.KeyDownEvent,
            (sender, e) =>
            {
                if (e.Key == TriggerKey) e.Handled = ExecuteCommand();
            },
            RoutingStrategies.Tunnel));
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        Disposables?.Dispose();
    }
}