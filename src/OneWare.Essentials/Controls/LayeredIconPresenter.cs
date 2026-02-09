using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Controls;

public class LayeredIconPresenter : Control
{
    private IDisposable? _baseSubscription;
    private IImage? _currentBaseIcon;
    private readonly List<OverlayState> _overlayStates = new();
    private IProjectEntry? _overlayEntry;
    private IProjectRoot? _overlayRoot;

    public static readonly StyledProperty<IconModel?> ValueProperty =
        AvaloniaProperty.Register<LayeredIconPresenter, IconModel?>(nameof(Value));

    public static readonly StyledProperty<IProjectExplorerNode?> OverlaySourceProperty =
        AvaloniaProperty.Register<LayeredIconPresenter, IProjectExplorerNode?>(nameof(OverlaySource));

    public IconModel? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IProjectExplorerNode? OverlaySource
    {
        get => GetValue(OverlaySourceProperty);
        set => SetValue(OverlaySourceProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == OverlaySourceProperty)
        {
            // Reset and resubscribe when the bound Model changes (row recycling)
            UpdateSubscriptions();
        }
    }

    private void UpdateSubscriptions()
    {
        Cleanup();

        var model = Value;
        if (model?.IconObservable != null)
        {
            _baseSubscription = model.IconObservable.Subscribe(img =>
            {
                _currentBaseIcon = (IImage?)img;
                Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
            });
        }
        else
        {
            _currentBaseIcon = model?.Icon;
        }

        _overlayEntry = OverlaySource as IProjectEntry;
        _overlayRoot = _overlayEntry?.Root;

        if (_overlayRoot != null)
            _overlayRoot.EntryOverlaysChanged += OnEntryOverlaysChanged;

        UpdateOverlays();
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

            foreach (var overlay in _overlayStates)
            {
                if (overlay.Image == null) continue;
                DrawOverlay(context, overlay, viewPort);
            }
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
        ClearOverlayStates();
        if (_overlayRoot != null)
            _overlayRoot.EntryOverlaysChanged -= OnEntryOverlaysChanged;
        _overlayEntry = null;
        _overlayRoot = null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Cleanup();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Value != null || OverlaySource != null) UpdateSubscriptions();
    }

    private void OnEntryOverlaysChanged(object? sender, ProjectEntryOverlayChangedEventArgs e)
    {
        if (_overlayEntry == null || !ReferenceEquals(e.Entry, _overlayEntry)) return;
        UpdateOverlays();
    }

    private void UpdateOverlays()
    {
        ClearOverlayStates();

        var overlays = new List<IconLayer>();
        if (_overlayEntry != null) overlays.AddRange(_overlayEntry.GetIconOverlays());

        foreach (var overlay in overlays)
        {
            if (overlay.IconObservable != null)
            {
                var state = new OverlayState(overlay);
                state.Subscription = overlay.IconObservable.Subscribe(img =>
                {
                    state.Image = (IImage?)img;
                    Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
                });
                _overlayStates.Add(state);
            }
            else
            {
                _overlayStates.Add(new OverlayState(overlay) { Image = overlay.Icon });
            }
        }

        InvalidateVisual();
    }

    private void ClearOverlayStates()
    {
        foreach (var state in _overlayStates)
            state.Subscription?.Dispose();
        _overlayStates.Clear();
    }

    private static void DrawOverlay(DrawingContext context, OverlayState overlay, Rect destRect)
    {
        var image = overlay.Image;
        if (image == null) return;

        var layer = overlay.Layer;
        var size = ResolveOverlaySize(layer, destRect.Size);
        var position = ResolveOverlayPosition(layer, destRect, size);

        var sourceRect = new Rect(image.Size);
        var dest = new Rect(position, size);
        context.DrawImage(image, sourceRect, dest);
    }

    private static Size ResolveOverlaySize(IconLayer layer, Size baseSize)
    {
        if (layer.Size.HasValue)
            return new Size(layer.Size.Value, layer.Size.Value);

        var size = Math.Min(baseSize.Width, baseSize.Height) * layer.SizeRatio;
        return new Size(size, size);
    }

    private static Point ResolveOverlayPosition(IconLayer layer, Rect destRect, Size overlaySize)
    {
        var x = layer.HorizontalAlignment switch
        {
            HorizontalAlignment.Left => destRect.X + layer.Margin.Left,
            HorizontalAlignment.Center => destRect.X + (destRect.Width - overlaySize.Width) / 2 +
                                           (layer.Margin.Left - layer.Margin.Right) / 2,
            HorizontalAlignment.Right => destRect.Right - overlaySize.Width - layer.Margin.Right,
            _ => destRect.X + layer.Margin.Left
        };

        var y = layer.VerticalAlignment switch
        {
            VerticalAlignment.Top => destRect.Y + layer.Margin.Top,
            VerticalAlignment.Center => destRect.Y + (destRect.Height - overlaySize.Height) / 2 +
                                         (layer.Margin.Top - layer.Margin.Bottom) / 2,
            VerticalAlignment.Bottom => destRect.Bottom - overlaySize.Height - layer.Margin.Bottom,
            _ => destRect.Y + layer.Margin.Top
        };

        return new Point(x, y);
    }

    private sealed class OverlayState(IconLayer layer)
    {
        public IconLayer Layer { get; } = layer;
        public IImage? Image { get; set; }
        public IDisposable? Subscription { get; set; }
    }
}
