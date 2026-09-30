using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OneWare.Settings.ViewModels.SettingTypes;

namespace OneWare.Settings.Views.SettingTypes;

public partial class SliderSettingView : UserControl
{
    public SliderSettingView()
    {
        InitializeComponent();
    }

    private void ValueTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        (DataContext as SliderSettingViewModel)?.CommitText();
    }

    private void ValueTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) (DataContext as SliderSettingViewModel)?.CommitText();
    }
}