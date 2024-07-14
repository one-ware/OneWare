using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace OneWare.Essentials.Controls;

public class HotkeyTextBox : TextBox
{
    public static readonly StyledProperty<KeyGesture?> CapturedKeyGestureProperty =
        AvaloniaProperty.Register<HotkeyTextBox, KeyGesture?>(nameof(CapturedKeyGesture), null, false,
            BindingMode.TwoWay);

    public HotkeyTextBox()
    {
        CaretBrush = Brushes.Transparent;
        KeyDown += OnKeyDownHandler;
    }

    public KeyGesture? CapturedKeyGesture
    {
        get => GetValue(CapturedKeyGestureProperty);
        set => SetValue(CapturedKeyGestureProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TextBox);

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CapturedKeyGestureProperty) Text = CapturedKeyGesture?.ToString();
    }

    private void OnKeyDownHandler(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.Escape) return;

        CapturedKeyGesture =
            new KeyGesture(e.Key, (CapturedKeyGesture?.KeyModifiers ?? KeyModifiers.None) | e.KeyModifiers);

        e.Handled = true;
    }
}