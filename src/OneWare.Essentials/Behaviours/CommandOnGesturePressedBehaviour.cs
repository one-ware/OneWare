using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OneWare.Essentials.Behaviours;

public class CommandOnGesturePressedBehaviour : CommandBasedBehaviour
{
    public static readonly StyledProperty<KeyGesture?> TriggerGestureProperty =
        AvaloniaProperty.Register<CommandOnKeyPressedBehaviour, KeyGesture?>(nameof(TriggerGesture));
    
    public static readonly StyledProperty<bool> HandledEventsTooProperty =
        AvaloniaProperty.Register<CommandOnKeyPressedBehaviour, bool>(nameof(HandledEventsToo));
    
    public KeyGesture? TriggerGesture
    {
        get => GetValue(TriggerGestureProperty);
        set => SetValue(TriggerGestureProperty, value);
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
                var gesture = new KeyGesture(e.Key, e.KeyModifiers);
                if (gesture == TriggerGesture) e.Handled = ExecuteCommand();
            },
            RoutingStrategies.Tunnel, HandledEventsToo));
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        Disposables?.Dispose();
    }
}