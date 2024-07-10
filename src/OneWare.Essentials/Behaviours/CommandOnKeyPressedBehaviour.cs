using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OneWare.Essentials.Behaviours;

public class CommandOnKeyPressedBehaviour : CommandBasedBehaviour
{
    public static readonly StyledProperty<Key?> TriggerKeyProperty =
        AvaloniaProperty.Register<CommandOnKeyPressedBehaviour, Key?>(nameof(TriggerKey));
    
    public static readonly StyledProperty<bool> HandledEventsTooProperty =
        AvaloniaProperty.Register<CommandOnKeyPressedBehaviour, bool>(nameof(HandledEventsToo));
    
    public Key? TriggerKey
    {
        get => GetValue(TriggerKeyProperty);
        set => SetValue(TriggerKeyProperty, value);
    }
    
    public bool HandledEventsToo
    {
        get => GetValue(HandledEventsTooProperty);
        set => SetValue(HandledEventsTooProperty, value);
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
            RoutingStrategies.Tunnel, HandledEventsToo));
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        Disposables?.Dispose();
    }
}