using System.Reactive.Linq;
using Avalonia.Controls;
using OneWare.Shared.Controls;

namespace OneWare.Core.Views.Windows;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    public async Task ShowVirtualDialogAsync(FlexibleWindow window)
    {
        DialogControlPanel.IsVisible = true;
        DialogControl.Content = window;
        DialogControl.Width = window.PrefWidth;
        DialogControl.Height = window.PrefHeight;
        DialogControl.Background = window.WindowBackground;
        await Observable.FromEventPattern(h => window.Closed += h, h => window.Closed -= h).Take(1).GetAwaiter();
        DialogControl.Content = null;
        DialogControlPanel.IsVisible = false;
    }
}