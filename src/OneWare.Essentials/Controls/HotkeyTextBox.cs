using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace OneWare.Essentials.Controls;

public class HotkeyTextBox : TextBox
{
    public static readonly StyledProperty<KeyGesture?> CapturedKeyGestureProperty =
        AvaloniaProperty.Register<HotkeyTextBox, KeyGesture?>(nameof(CapturedKeyGesture), null, false, BindingMode.TwoWay);

    public KeyGesture? CapturedKeyGesture
    {
        get => GetValue(CapturedKeyGestureProperty);
        set => SetValue(CapturedKeyGestureProperty, value);
    }
    
    public HotkeyTextBox()
    {
        CaretBrush = Brushes.Transparent;
        this.KeyDown += OnKeyDownHandler;
    }

    protected override Type StyleKeyOverride => typeof(TextBox);

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CapturedKeyGestureProperty)
        {
            this.Text = CapturedKeyGesture?.ToString();
        }
    }

    private void OnKeyDownHandler(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.Escape)
        {
            return;
        }

        CapturedKeyGesture = new KeyGesture(e.Key, (CapturedKeyGesture?.KeyModifiers ?? KeyModifiers.None) | e.KeyModifiers);
        
        e.Handled = true;
    }
}