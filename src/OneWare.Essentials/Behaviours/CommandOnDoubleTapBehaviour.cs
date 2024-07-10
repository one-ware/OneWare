using System.Reactive.Disposables;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OneWare.Essentials.Behaviours
{
    public class CommandOnDoubleTapBehaviour : CommandBasedBehaviour
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