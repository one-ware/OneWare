using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;

namespace OneWare.Essentials.Behaviors;

/// <summary>
/// This behavior fixes the avalonia textbox bug, that the validation errors won't
/// be cleared, if the new value equals the last valid input.
/// </summary>
public class ClearValidationErrorsLostFocusBehavior : Trigger<TextBox>
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ClearValidationErrorsLostFocusBehavior, string?>(nameof(Text));
    
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
    
    protected override void OnAttached()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.LostFocus += OnLostFocus;
        }
        base.OnAttached();
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.LostFocus -= OnLostFocus;
        }
        base.OnDetaching();
    }

    private void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (AssociatedObject == null) 
            return;

        if (DataValidationErrors.GetHasErrors(AssociatedObject) && AssociatedObject.Text == Text)
        {
            DataValidationErrors.ClearErrors(AssociatedObject);
        }
    }
}