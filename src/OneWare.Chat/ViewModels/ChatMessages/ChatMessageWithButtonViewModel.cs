using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;

namespace OneWare.Chat.ViewModels.ChatMessages;

public class ChatMessageWithButtonViewModel : ObservableObject, IChatMessage
{
    public ChatMessageWithButtonViewModel(ChatButtonEvent buttonEvent)
    {
        Event = buttonEvent;
    }
    
    public ChatButtonEvent Event { get; }
}