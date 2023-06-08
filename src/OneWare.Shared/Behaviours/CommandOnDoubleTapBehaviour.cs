using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OneWare.Shared.Behaviours
{
    public class CommandOnDoubleTapBehaviour : CommandBasedBehavior
    {
        private CompositeDisposable? Disposables { get; set; }

        protected override void OnAttached()
        {
            if (AssociatedObject == null) return;
            Disposables = new CompositeDisposable();

            base.OnAttached();

            Disposables.Add(AssociatedObject.AddDisposableHandler(
                InputElement.DoubleTappedEvent,
                (sender, e) =>
                {
                    e.Handled = ExecuteCommand();
                },
                RoutingStrategies.Bubble, true));
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            Disposables?.Dispose();
        }
    }
}