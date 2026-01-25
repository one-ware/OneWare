using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.ChatBot.ViewModels;

public partial class ChatMessageViewModel : ObservableObject
{
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private bool _isStreaming;

    public ChatMessageViewModel(string author, IImage icon, bool isUser)
    {
        Author = author;
        Icon = icon;
        IsUser = isUser;
        Timestamp = DateTimeOffset.Now;
    }

    public IImage Icon { get; }
    
    public string Author { get; }

    public bool IsUser { get; }

    public bool IsAssistant => !IsUser;

    public DateTimeOffset Timestamp { get; }
}
