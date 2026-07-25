using System.ComponentModel;
using Avalonia.Controls;
using OneWare.Chat.ViewModels.ChatMessages;

namespace OneWare.Chat.Views.ChatMessages;

public partial class ChatMessageToolView : UserControl
{
    private ChatMessageToolViewModel? _subscribed;

    public ChatMessageToolView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribed != null)
            _subscribed.PropertyChanged -= OnViewModelPropertyChanged;

        _subscribed = DataContext as ChatMessageToolViewModel;

        if (_subscribed != null)
            _subscribed.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ChatMessageToolViewModel vm) return;
        if (e.PropertyName != nameof(ChatMessageToolViewModel.ToolOutput)) return;
        if (!vm.IsToolRunning) return;

        OutputScrollViewer.ScrollToEnd();
    }
}
