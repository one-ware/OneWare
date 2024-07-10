using System.Reactive.Disposables;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OneWare.Essentials.Behaviours
{
    public class CommandOnEnterBehaviour : CommandBasedBehaviour
    {
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
                    if (e.Key == Key.Enter) e.Handled = ExecuteCommand();
                },
                RoutingStrategies.Tunnel));
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            Disposables?.Dispose();
        }
    }
}