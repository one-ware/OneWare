using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactions.Custom;
using Avalonia.Xaml.Interactivity;
using OneWare.ChatBot.ViewModels;

namespace OneWare.ChatBot.Behaviors;

public class CommandOnEnterBehavior : ExecuteCommandRoutedEventBehaviorBase
{
    protected override System.IDisposable OnAttachedToVisualTreeOverride()
    {
        var control = SourceControl ?? AssociatedObject;
        var dispose = control?
            .AddDisposableHandler(
                InputElement.KeyDownEvent,
                OnKeyDown,
                EventRoutingStrategy);

        if (dispose is not null)
        {
            return dispose;
        }

        return DisposableAction.Empty;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        if (e.Handled)
        {
            return;
        }

        if (ExecuteCommand())
        {
            e.Handled = MarkAsHandled;
        }
    }
}
