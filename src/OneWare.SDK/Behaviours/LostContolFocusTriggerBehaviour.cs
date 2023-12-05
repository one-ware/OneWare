using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;

namespace OneWare.SDK.Behaviours;

public class LostContolFocusTriggerBehaviour : Trigger<Control>
{
    private CompositeDisposable? Disposables { get; set; }

    protected override void OnAttached()
    {
        if (AssociatedObject == null) return;
        Disposables = new CompositeDisposable();

        base.OnAttached();
        
        // Disposables.Add(AssociatedObject.AddDisposableHandler(
        //     InputElement.KeyDownEvent,
        //     (sender, e) =>
        //     {
        //         if (e.Key == TriggerKey) e.Handled = ExecuteCommand();
        //     },
        //     RoutingStrategies.Tunnel, HandledEventsToo));
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        Disposables?.Dispose();
    }
}