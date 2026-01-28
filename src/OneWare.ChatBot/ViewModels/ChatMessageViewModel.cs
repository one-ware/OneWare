using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.ChatBot.ViewModels;

public class ChatMessageViewModel : ObservableObject
{
    public string Message
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public bool IsStreaming
    {
        get;
        set => SetProperty(ref field, value);
    }

    public ChatMessageViewModel(string author, bool isUser)
    {
        Author = author;
        IsUser = isUser;
        Timestamp = DateTimeOffset.Now;
    }

    public string Author { get; }

    public bool IsUser { get; }

    public bool IsAssistant => !IsUser;

    public bool IsToolMessage
    {
        get;
        set => SetProperty(ref field, value);
    }

    public DateTimeOffset Timestamp { get; }
}
