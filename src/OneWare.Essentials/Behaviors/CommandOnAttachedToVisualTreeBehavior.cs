using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace OneWare.Essentials.Behaviors;

/// <summary>
///     Executes a command when the associated control is attached to the visual tree.
///     Useful for lazy loading content that is only needed once the control is actually shown.
/// </summary>
public class CommandOnAttachedToVisualTreeBehavior : CommandBasedBehavior
{
    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject == null) return;

        AssociatedObject.AttachedToVisualTree += OnAttachedToVisualTree;

        if (AssociatedObject.IsAttachedToVisualTree())
            // Bindings may not be applied yet when the behavior itself attaches
            Dispatcher.UIThread.Post(() =>
            {
                if (AssociatedObject?.IsAttachedToVisualTree() ?? false) ExecuteCommand();
            });
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
            AssociatedObject.AttachedToVisualTree -= OnAttachedToVisualTree;

        base.OnDetaching();
    }

    private void OnAttachedToVisualTree(object? sender, EventArgs e)
    {
        ExecuteCommand();
    }
}
