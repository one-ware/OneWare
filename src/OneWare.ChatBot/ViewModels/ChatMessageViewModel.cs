using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.ChatBot.ViewModels;

public class ChatMessageViewModel(string author, IImage icon) : ObservableObject
{
    private string _message = string.Empty;
    
    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public IImage Icon { get; } = icon;
    
    public string Author { get; } = author;
}