using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;

namespace OneWare.Essentials.Controls;

/// <summary>
///     A generic single-selection segmented control that lays its items out horizontally.
///     Items can be plain objects or <see cref="SegmentedControlItem" /> instances.
/// </summary>
public class SegmentedControl : SelectingItemsControl
{
    private static readonly FuncTemplate<Panel?> DefaultPanel =
        new(() => new StackPanel { Orientation = Orientation.Horizontal });

    static SegmentedControl()
    {
        SelectionModeProperty.OverrideDefaultValue<SegmentedControl>(
            SelectionMode.Single | SelectionMode.AlwaysSelected);
        ItemsPanelProperty.OverrideDefaultValue<SegmentedControl>(DefaultPanel);
    }

    protected override Type StyleKeyOverride => typeof(SegmentedControl);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.Source is Visual source && e.GetCurrentPoint(source).Properties.IsLeftButtonPressed)
            e.Handled = UpdateSelectionFromEventSource(e.Source);
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        return new SegmentedControlItem();
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        return NeedsContainer<SegmentedControlItem>(item, out recycleKey);
    }
}

/// <summary>
///     Container for an item hosted inside a <see cref="SegmentedControl" />.
/// </summary>
public class SegmentedControlItem : ContentControl, ISelectable
{
    public static readonly StyledProperty<bool> IsSelectedProperty =
        SelectingItemsControl.IsSelectedProperty.AddOwner<SegmentedControlItem>();

    static SegmentedControlItem()
    {
        SelectableMixin.Attach<SegmentedControlItem>(IsSelectedProperty);
        PressedMixin.Attach<SegmentedControlItem>();
        FocusableProperty.OverrideDefaultValue<SegmentedControlItem>(true);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(SegmentedControlItem);
}
