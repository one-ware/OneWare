using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Controls;

public class UiExtensionControl : ContentControl
{
    public static readonly StyledProperty<object?> ContextProperty =
        AvaloniaProperty.Register<UiExtensionControl, object?>(nameof(Context), null, false, BindingMode.OneWay);

    public object? Context
    {
        get => GetValue(ContextProperty);
        set => SetValue(ContextProperty, value);
    }

    public static readonly StyledProperty<UiExtension?> UiExtensionProperty =
        AvaloniaProperty.Register<UiExtensionControl, UiExtension?>(nameof(UiExtension));

    public UiExtension? UiExtension
    {
        get => GetValue(UiExtensionProperty);
        set => SetValue(UiExtensionProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ContentControl);
    

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == UiExtensionProperty)
        {
            ConstructContent();
        }

        if (change.Property == ContextProperty)
        {
            ConstructContent();
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        //ConstructContent();
    }

    private void ConstructContent()
    {
        if (UiExtension == null)
        {
            Content = null;
            return;
        }

        var extension = UiExtension.CreateUiExtension(Context);
        if (extension != null)
        {
            Content = extension;
        }
           
    }
}