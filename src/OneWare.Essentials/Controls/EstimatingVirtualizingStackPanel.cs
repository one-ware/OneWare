using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace OneWare.Essentials.Controls;

/// <summary>
/// A vertical virtualizing panel that keeps a per-item height cache to provide a stable,
/// content-accurate scroll extent.
/// </summary>
/// <remarks>
/// Unlike <see cref="VirtualizingStackPanel"/>, which estimates the total scroll extent from the
/// running average height of the currently realized items (causing the scrollbar to jump when
/// items vary a lot in height), this panel tracks every item's height individually:
/// <list type="bullet">
/// <item>Realized items use their real measured height.</item>
/// <item>Unrealized items use an estimate, ideally provided by <see cref="IEstimatedHeightItem"/>
/// (e.g. derived from a chat message's markdown), otherwise <see cref="EstimatedItemHeight"/>.</item>
/// </list>
/// The reported <see cref="Layoutable.DesiredSize"/> is the sum of all cached heights, so the
/// scrollbar reflects the real content size. Realized children are registered with the ancestor
/// <see cref="IScrollAnchorProvider"/> so the surrounding <see cref="ScrollViewer"/> keeps the
/// visible content anchored when a cached estimate is replaced by a real measurement.
/// </remarks>
public class EstimatingVirtualizingStackPanel : VirtualizingPanel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<EstimatingVirtualizingStackPanel, double>(nameof(Spacing));

    public static readonly StyledProperty<double> EstimatedItemHeightProperty =
        AvaloniaProperty.Register<EstimatingVirtualizingStackPanel, double>(
            nameof(EstimatedItemHeight), 48);

    public static readonly StyledProperty<double> CacheLengthProperty =
        AvaloniaProperty.Register<EstimatingVirtualizingStackPanel, double>(
            nameof(CacheLength), 0.5);

    private readonly List<double> _sizes = new();
    private readonly List<bool> _measured = new();
    private readonly Dictionary<int, RealizedContainer> _realized = new();
    private readonly List<int> _scratch = new();

    private Rect _viewport;
    private double _lastWidth = 400;
    private IScrollAnchorProvider? _scrollAnchorProvider;

    static EstimatingVirtualizingStackPanel()
    {
        AffectsMeasure<EstimatingVirtualizingStackPanel>(SpacingProperty);
    }

    /// <summary>Gets or sets the gap, in pixels, between successive items.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>
    /// Fallback estimated height (pixels) for items that do not implement
    /// <see cref="IEstimatedHeightItem"/>.
    /// </summary>
    public double EstimatedItemHeight
    {
        get => GetValue(EstimatedItemHeightProperty);
        set => SetValue(EstimatedItemHeightProperty, value);
    }

    /// <summary>
    /// How many viewports worth of items to realize on each side of the visible area. A larger
    /// value reduces realization churn (and markdown re-rendering) while scrolling.
    /// </summary>
    public double CacheLength
    {
        get => GetValue(CacheLengthProperty);
        set => SetValue(CacheLengthProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var items = Items;
        var count = items.Count;
        SyncCache(count);

        if (count == 0)
        {
            RecycleAll();
            return default;
        }

        var width = double.IsInfinity(availableSize.Width) ? _lastWidth : availableSize.Width;
        if (System.Math.Abs(width - _lastWidth) > 0.5)
        {
            _lastWidth = width;
            for (var i = 0; i < _sizes.Count; i++)
                if (!_measured[i])
                    _sizes[i] = EstimateAt(i);
        }
        else
        {
            _lastWidth = width;
        }
        var spacing = Spacing;

        var viewportStart = _viewport.Top;
        var viewportEnd = _viewport.Bottom;
        var viewportHeight = viewportEnd - viewportStart;

        // Before any effective viewport has been reported we still realize the first item so that
        // the layout system has something to measure; a real pass follows once the viewport is known.
        if (viewportHeight <= 0)
        {
            viewportStart = 0;
            viewportEnd = 0;
        }

        var cache = System.Math.Max(0, CacheLength) * viewportHeight;
        var realizeStart = viewportStart - cache;
        var realizeEnd = viewportEnd + cache;

        var first = IndexAt(realizeStart, spacing);
        var keep = new HashSet<int>();
        var maxWidth = 0.0;

        var index = first;
        var u = TopOf(first, spacing);

        while (index < count)
        {
            var container = Realize(index);
            container.Measure(new Size(width, double.PositiveInfinity));
            var h = container.DesiredSize.Height;
            _sizes[index] = h;
            _measured[index] = true;
            maxWidth = System.Math.Max(maxWidth, container.DesiredSize.Width);
            keep.Add(index);

            u += h + spacing;
            index++;

            if (u >= realizeEnd)
                break;
        }

        RecycleExcept(keep);

        return new Size(System.Math.Max(width, maxWidth), TotalHeight(spacing));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var spacing = Spacing;

        foreach (var pair in _realized)
        {
            var i = pair.Key;
            var container = pair.Value.Container;
            var top = TopOf(i, spacing);
            container.Arrange(new Rect(0, top, finalSize.Width, _sizes[i]));
            _scrollAnchorProvider?.RegisterAnchorCandidate(container);
        }

        return new Size(finalSize.Width, System.Math.Max(finalSize.Height, TotalHeight(spacing)));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        EffectiveViewportChanged += OnEffectiveViewportChanged;
        _scrollAnchorProvider = this.FindAncestorOfType<IScrollAnchorProvider>();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        EffectiveViewportChanged -= OnEffectiveViewportChanged;
        _scrollAnchorProvider = null;
    }

    protected override void OnItemsChanged(IReadOnlyList<object?> items, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
            {
                var start = e.NewStartingIndex;
                var n = e.NewItems!.Count;
                ShiftRealized(start, n);
                for (var k = 0; k < n; k++)
                {
                    _sizes.Insert(start + k, EstimateAt(start + k));
                    _measured.Insert(start + k, false);
                }
                break;
            }
            case NotifyCollectionChangedAction.Remove:
            {
                var start = e.OldStartingIndex;
                var n = e.OldItems!.Count;
                for (var i = start; i < start + n; i++)
                    RecycleIndex(i);
                if (start < _sizes.Count)
                {
                    var removable = System.Math.Min(n, _sizes.Count - start);
                    _sizes.RemoveRange(start, removable);
                    _measured.RemoveRange(start, removable);
                }
                ShiftRealized(start + n, -n);
                break;
            }
            case NotifyCollectionChangedAction.Replace:
            {
                var start = e.OldStartingIndex;
                var n = e.OldItems!.Count;
                for (var i = start; i < start + n; i++)
                {
                    RecycleIndex(i);
                    if (i < _sizes.Count)
                    {
                        _sizes[i] = EstimateAt(i);
                        _measured[i] = false;
                    }
                }
                break;
            }
            default:
            {
                RecycleAll();
                _sizes.Clear();
                _measured.Clear();
                SyncCache(items.Count);
                break;
            }
        }

        InvalidateMeasure();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EstimatedItemHeightProperty || change.Property == CacheLengthProperty)
            InvalidateMeasure();
    }

    private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        var oldStart = _viewport.Top;
        var oldEnd = _viewport.Bottom;

        _viewport = e.EffectiveViewport.Intersect(new Rect(Bounds.Size));

        if (!AreClose(oldStart, _viewport.Top) || !AreClose(oldEnd, _viewport.Bottom))
            InvalidateMeasure();
    }

    private static bool AreClose(double a, double b) => System.Math.Abs(a - b) < 0.5;

    // --- Height cache helpers -------------------------------------------------------------

    private void SyncCache(int count)
    {
        if (_sizes.Count == count)
            return;

        if (count < _sizes.Count)
        {
            _sizes.RemoveRange(count, _sizes.Count - count);
            _measured.RemoveRange(count, _measured.Count - count);
        }
        else
        {
            for (var k = _sizes.Count; k < count; k++)
            {
                _sizes.Add(EstimateAt(k));
                _measured.Add(false);
            }
        }
    }

    private double EstimateAt(int index)
    {
        var item = index >= 0 && index < Items.Count ? Items[index] : null;
        var height = item is IEstimatedHeightItem e ? e.EstimateHeight(_lastWidth) : EstimatedItemHeight;
        return height > 1 ? height : 1;
    }

    private double TopOf(int index, double spacing)
    {
        var u = 0.0;
        var max = System.Math.Min(index, _sizes.Count);
        for (var i = 0; i < max; i++)
            u += _sizes[i] + spacing;
        return u;
    }

    private double TotalHeight(double spacing)
    {
        var total = 0.0;
        for (var i = 0; i < _sizes.Count; i++)
            total += _sizes[i];
        if (_sizes.Count > 1)
            total += spacing * (_sizes.Count - 1);
        return total;
    }

    private int IndexAt(double position, double spacing)
    {
        if (position <= 0 || _sizes.Count == 0)
            return 0;

        var u = 0.0;
        for (var i = 0; i < _sizes.Count; i++)
        {
            var bottom = u + _sizes[i];
            if (bottom > position)
                return i;
            u = bottom + spacing;
        }

        return _sizes.Count - 1;
    }

    // --- Realization ----------------------------------------------------------------------

    private Control Realize(int index)
    {
        if (_realized.TryGetValue(index, out var existing))
            return existing.Container;

        var item = Items[index];
        var generator = ItemContainerGenerator!;
        Control container;
        object? recycleKey = null;
        var ownContainer = false;

        if (generator.NeedsContainer(item, index, out recycleKey))
        {
            container = generator.CreateContainer(item, index, recycleKey);
        }
        else
        {
            ownContainer = true;
            container = (Control)item!;
        }

        generator.PrepareItemContainer(container, item, index);
        AddInternalChild(container);
        generator.ItemContainerPrepared(container, item, index);

        _realized[index] = new RealizedContainer(container, recycleKey, ownContainer);
        return container;
    }

    private void RecycleExcept(HashSet<int> keep)
    {
        if (_realized.Count == 0)
            return;

        _scratch.Clear();
        foreach (var index in _realized.Keys)
        {
            if (!keep.Contains(index))
                _scratch.Add(index);
        }

        foreach (var index in _scratch)
            RecycleIndex(index);
    }

    private void RecycleAll()
    {
        if (_realized.Count == 0)
            return;

        _scratch.Clear();
        foreach (var index in _realized.Keys)
            _scratch.Add(index);

        foreach (var index in _scratch)
            RecycleIndex(index);
    }

    private void RecycleIndex(int index)
    {
        if (!_realized.TryGetValue(index, out var realized))
            return;

        _realized.Remove(index);
        _scrollAnchorProvider?.UnregisterAnchorCandidate(realized.Container);

        if (realized.IsOwnContainer)
        {
            RemoveInternalChild(realized.Container);
        }
        else
        {
            ItemContainerGenerator!.ClearItemContainer(realized.Container);
            RemoveInternalChild(realized.Container);
        }
    }

    private void ShiftRealized(int fromIndex, int delta)
    {
        if (delta == 0 || _realized.Count == 0)
            return;

        var generator = ItemContainerGenerator;
        var moved = new List<KeyValuePair<int, RealizedContainer>>();

        _scratch.Clear();
        foreach (var pair in _realized)
        {
            if (pair.Key >= fromIndex)
            {
                moved.Add(pair);
                _scratch.Add(pair.Key);
            }
        }

        foreach (var index in _scratch)
            _realized.Remove(index);

        foreach (var pair in moved)
        {
            var newIndex = pair.Key + delta;
            _realized[newIndex] = pair.Value;
            generator?.ItemContainerIndexChanged(pair.Value.Container, pair.Key, newIndex);
        }
    }

    // --- VirtualizingPanel abstract members -----------------------------------------------

    protected override Control? ScrollIntoView(int index)
    {
        if (index < 0 || index >= Items.Count)
            return null;

        var container = Realize(index);
        container.Measure(new Size(_lastWidth, double.PositiveInfinity));
        _sizes[index] = container.DesiredSize.Height;
        _measured[index] = true;
        container.BringIntoView();
        return container;
    }

    protected override Control? ContainerFromIndex(int index)
    {
        return _realized.TryGetValue(index, out var realized) ? realized.Container : null;
    }

    protected override int IndexFromContainer(Control container)
    {
        foreach (var pair in _realized)
        {
            if (ReferenceEquals(pair.Value.Container, container))
                return pair.Key;
        }

        return -1;
    }

    protected override IEnumerable<Control>? GetRealizedContainers()
    {
        var result = new List<Control>(_realized.Count);
        foreach (var pair in _realized)
            result.Add(pair.Value.Container);
        return result;
    }

    protected override IInputElement? GetControl(NavigationDirection direction, IInputElement? from, bool wrap)
    {
        var count = Items.Count;
        if (count == 0)
            return null;

        var fromIndex = from is Control c ? IndexFromContainer(c) : -1;
        int toIndex;

        switch (direction)
        {
            case NavigationDirection.First:
                toIndex = 0;
                break;
            case NavigationDirection.Last:
                toIndex = count - 1;
                break;
            case NavigationDirection.Next:
            case NavigationDirection.Down:
                toIndex = fromIndex + 1;
                break;
            case NavigationDirection.Previous:
            case NavigationDirection.Up:
                toIndex = fromIndex - 1;
                break;
            default:
                return null;
        }

        if (wrap)
        {
            if (toIndex < 0)
                toIndex = count - 1;
            else if (toIndex >= count)
                toIndex = 0;
        }

        if (toIndex < 0 || toIndex >= count)
            return null;

        return ScrollIntoView(toIndex);
    }

    private readonly struct RealizedContainer
    {
        public RealizedContainer(Control container, object? recycleKey, bool isOwnContainer)
        {
            Container = container;
            RecycleKey = recycleKey;
            IsOwnContainer = isOwnContainer;
        }

        public Control Container { get; }
        public object? RecycleKey { get; }
        public bool IsOwnContainer { get; }
    }
}
