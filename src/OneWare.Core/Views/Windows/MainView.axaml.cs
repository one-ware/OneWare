using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OneWare.Shared.Controls;

namespace OneWare.Core.Views.Windows;

public partial class MainView : UserControl
{
    private readonly Stack<FlexibleWindow> _windowStack = new();

    public MainView()
    {
        InitializeComponent();
    }
    
    public async Task ShowVirtualDialogAsync(FlexibleWindow window)
    {
        _windowStack.Push(window);
        
        SetVirtualDialog(window);
        
        await Observable.FromEventPattern(h => window.Closed += h, h => window.Closed -= h).Take(1).GetAwaiter();
        
        _windowStack.Pop();
        
        if (_windowStack.Any())
        {
            await ShowVirtualDialogAsync(_windowStack.Pop());
        }
        else
        {
            DialogControl.Content = null;
            DialogControlPanel.IsVisible = false;
        }
    }

    private void SetVirtualDialog(FlexibleWindow window)
    {
        DialogControlPanel.IsVisible = true;
        DialogControl.Content = window;
        DialogControl.Width = window.PrefWidth < this.Bounds.Width ? window.PrefWidth : this.Bounds.Width;
        DialogControl.Height = window.PrefHeight + 40 < this.Bounds.Height ? window.PrefHeight : this.Bounds.Height - 40;
        DialogControl.Background = window.WindowBackground;
        DialogTitle.Text = window.Title;
        if (window.CustomIcon != null)
        {
            DialogIcon.Source = window.CustomIcon;
            DialogIcon.IsVisible = true;
        }
        else
        {
            DialogIcon.IsVisible = false;
        }
    }
    
    private void DialogCloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if(_windowStack.Any())
            _windowStack.Peek().Close();
    }
}