using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace OneWare.Essentials.Controls;

/// <summary>
/// Redraws a control on every animation frame while it is visible and attached to a window. All loops share one
/// clock, so indicators that use the same cycle duration stay in phase.
/// </summary>
internal sealed class AnimationFrameLoop(Visual owner)
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    private readonly List<Visual> _watchedAncestors = [];
    private bool _frameRequested;

    /// <summary>Position within a cycle of the given duration, from 0 to 1.</summary>
    public static double GetProgress(TimeSpan cycleDuration)
    {
        var cycleMs = Math.Max(1, cycleDuration.TotalMilliseconds);
        return Clock.Elapsed.TotalMilliseconds % cycleMs / cycleMs;
    }

    public void Attach()
    {
        // The loop stops while the owner is hidden. Showing a hidden ancestor again only replays the
        // last recorded frame without calling Render, so visibility changes up the tree restart it.
        for (var ancestor = owner.GetVisualParent(); ancestor != null; ancestor = ancestor.GetVisualParent())
        {
            ancestor.PropertyChanged += OnPropertyChanged;
            _watchedAncestors.Add(ancestor);
        }

        owner.PropertyChanged += OnPropertyChanged;
        RequestNextFrame();
    }

    public void Detach()
    {
        owner.PropertyChanged -= OnPropertyChanged;
        foreach (var ancestor in _watchedAncestors)
            ancestor.PropertyChanged -= OnPropertyChanged;
        _watchedAncestors.Clear();
    }

    /// <summary>Call at the end of the owner's Render to schedule the next frame.</summary>
    public void RequestNextFrame()
    {
        if (_frameRequested || !owner.IsEffectivelyVisible) return;
        if (TopLevel.GetTopLevel(owner) is not { } topLevel) return;

        _frameRequested = true;
        topLevel.RequestAnimationFrame(_ =>
        {
            _frameRequested = false;
            owner.InvalidateVisual();
        });
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Visual.IsVisibleProperty || e.NewValue is not true) return;

        // Posted so IsEffectivelyVisible already reflects the change.
        Dispatcher.UIThread.Post(() =>
        {
            owner.InvalidateVisual();
            RequestNextFrame();
        });
    }
}
