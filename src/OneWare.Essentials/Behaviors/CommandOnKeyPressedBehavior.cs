using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OneWare.Essentials.Behaviors;

public class CommandOnKeyPressedBehavior : CommandBasedBehavior
{
    public static readonly StyledProperty<Key?> TriggerKeyProperty =
        AvaloniaProperty.Register<CommandOnKeyPressedBehavior, Key?>(nameof(TriggerKey));

    public static readonly StyledProperty<bool> HandledEventsTooProperty =
        AvaloniaProperty.Register<CommandOnKeyPressedBehavior, bool>(nameof(HandledEventsToo));

    public static readonly StyledProperty<RoutingStrategies> RoutesProperty =
        AvaloniaProperty.Register<CommandOnKeyPressedBehavior, RoutingStrategies>(nameof(Routes),
            RoutingStrategies.Bubble);

    public Key? TriggerKey
    {
        get => GetValue(TriggerKeyProperty);
        set => SetValue(TriggerKeyProperty, value);
    }

    public RoutingStrategies Routes
    {
        get => GetValue(RoutesProperty);
        set => SetValue(RoutesProperty, value);
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
            Routes, HandledEventsToo));
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        Disposables?.Dispose();
    }
}