using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Xaml.Interactivity;

namespace OneWare.Essentials.Behaviors;

public class ApplyAlternativeBindingIfNullBehavior : Behavior<Control>
{
    public static readonly StyledProperty<object?> OriginalValueProperty =
        AvaloniaProperty.Register<ApplyAlternativeBindingIfNullBehavior, object?>(nameof(OriginalValue), null, false,
            BindingMode.TwoWay);

    public static readonly StyledProperty<object?> AlternativeValueProperty =
        AvaloniaProperty.Register<ApplyAlternativeBindingIfNullBehavior, object?>(nameof(AlternativeValue));

    public object? OriginalValue
    {
        get => GetValue(OriginalValueProperty);
        set => SetValue(OriginalValueProperty, value);
    }

    public object? AlternativeValue
    {
        get => GetValue(AlternativeValueProperty);
        set => SetValue(AlternativeValueProperty, value);
    }

    protected override void OnAttachedToVisualTree()
    {
        ApplyAlternative();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == AlternativeValueProperty) ApplyAlternative();
    }

    public void ApplyAlternative()
    {
        if (OriginalValue is null && AlternativeValue is not null) OriginalValue = AlternativeValue;
    }
}