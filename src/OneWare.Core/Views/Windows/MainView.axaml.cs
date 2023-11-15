using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DynamicData.Binding;
using OneWare.Shared.Controls;
using OneWare.Shared.ViewModels;

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
        DialogControl.Height = double.NaN;
        DialogControl.Width = double.NaN;
        DialogControl.Content = window;
        
        if(!double.IsNaN(window.PrefWidth)) DialogControl.Width = window.PrefWidth < this.Bounds.Width ? window.PrefWidth : this.Bounds.Width;
        if(!double.IsNaN(window.PrefHeight)) DialogControl.Height = window.PrefHeight + 40 < this.Bounds.Height ? window.PrefHeight : this.Bounds.Height - 40;

        window.WhenValueChanged(x => x.Title).Subscribe(x => DialogTitle.Text = x);
        window.WhenValueChanged(x => x.Background).Subscribe(x => DialogControl.Background = x);
        
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
        if (_windowStack.Any())
        {
            if(_windowStack.Peek() is {DataContext: FlexibleWindowViewModelBase vm} window)
                vm.Close(window);
            else _windowStack.Peek().Close();
        }
    }
}