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
    private INotifyCollectionChanged? _currentOverlays;

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

        // 3. Handle Overlays (Listen for Add/Remove)
        if (model.IconOverlays != null)
        {
            _currentOverlays = model.IconOverlays;
            _currentOverlays.CollectionChanged += OnOverlaysChanged;
        }

        InvalidateVisual();
    }

    private void OnOverlaysChanged(object? sender, NotifyCollectionChangedEventArgs e) 
        => Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);

    public override void Render(DrawingContext context)
    {
        if (Value == null) return;

        var rect = new Rect(Bounds.Size);
        
        if (_currentBaseIcon != null)
        {
            context.DrawImage(_currentBaseIcon, rect);
        }
        
        if (Value.IconOverlays != null)
        {
            foreach (var overlay in Value.IconOverlays)
            {
                context.DrawImage(overlay, rect);
            }
        }
    }

    private void Cleanup()
    {
        _baseSubscription?.Dispose();
        _baseSubscription = null;
        if (_currentOverlays != null)
        {
            _currentOverlays.CollectionChanged -= OnOverlaysChanged;
            _currentOverlays = null;
        }
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