using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
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
        // 1. Cleanup old subscriptions
        Cleanup();

        if (model == null) return;

        // 2. Handle Base Icon (Observable has priority)
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

    public override void Render(DrawingContext context)
    {
        if (Value == null) return;

        var rect = new Rect(Bounds.Size);
        
        if (_currentBaseIcon != null)
        {
            context.DrawImage(_currentBaseIcon, rect);
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