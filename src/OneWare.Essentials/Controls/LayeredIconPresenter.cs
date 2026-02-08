using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using ExCSS;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Controls;

public class LayeredIconPresenter : Control
{
    private IDisposable? _baseSubscription;
    private IImage? _currentBaseIcon;

    public static readonly StyledProperty<IconModel?> ValueProperty =
        AvaloniaProperty.Register<LayeredIconPresenter, IconModel?>(nameof(Value));

    public IconModel? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            // Reset and resubscribe when the bound Model changes (row recycling)
            UpdateSubscriptions(change.GetNewValue<IconModel?>());
        }
    }

    private void UpdateSubscriptions(IconModel? model)
    {
        Cleanup();

        if (model == null) return;
        
        if (model.IconObservable != null)
        {
            _baseSubscription = model.IconObservable.Subscribe(img =>
            {
                _currentBaseIcon = (IImage?)img;
                Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
            });
        }
        else
        {
            _currentBaseIcon = model.Icon;
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Renders the control.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    public sealed override void Render(DrawingContext context)
    {
        var source = _currentBaseIcon;

        if (source != null && Bounds.Width > 0 && Bounds.Height > 0)
        {
            Rect viewPort = new Rect(Bounds.Size);
            Size sourceSize = source.Size;

            Vector scale = Stretch.Uniform.CalculateScaling(Bounds.Size, sourceSize);
            Size scaledSize = sourceSize * scale;
            Rect destRect = viewPort
                .CenterRect(new Rect(scaledSize))
                .Intersect(viewPort);
            Rect sourceRect = new Rect(sourceSize)
                .CenterRect(new Rect(destRect.Size / scale));

            context.DrawImage(source, sourceRect, destRect);
        }
    }

    /// <summary>
    /// Measures the control.
    /// </summary>
    /// <param name="availableSize">The available size.</param>
    /// <returns>The desired size of the control.</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        var source = _currentBaseIcon;
        var result = new Size();

        if (source != null)
        {
            result = Stretch.Uniform.CalculateSize(availableSize, source.Size);
        }

        return result;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var source = _currentBaseIcon;

        if (source != null)
        {
            var sourceSize = source.Size;
            var result = Stretch.Uniform.CalculateSize(finalSize, sourceSize);
            return result;
        }
        else
        {
            return new Size();
        }
    }

    private void Cleanup()
    {
        _baseSubscription?.Dispose();
        _baseSubscription = null;
        _currentBaseIcon = null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Cleanup();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Value != null) UpdateSubscriptions(Value);
    }
}