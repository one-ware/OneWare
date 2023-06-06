using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OneWare.Shared;

namespace OneWare.Core.Views.Windows;

public partial class MainView : UserControl
{
    public static readonly StyledProperty<Control?> DialogControlProperty =
        AvaloniaProperty.Register<MainView, Control?>(nameof(DialogControl));
    
    public Control? DialogControl
    {
        get => GetValue(DialogControlProperty);
        set => SetValue(DialogControlProperty, value);
    }
    
    public MainView()
    {
        InitializeComponent();
    }

    public async Task ShowVirtualDialogAsync(FlexibleWindow window)
    {
        DialogControl = window;
        await Observable.FromEventPattern(h => window.Closed += h, h => window.Closed -= h).Take(1).GetAwaiter();
        DialogControl = null;
    }
}