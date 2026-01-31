using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Chat.ViewModels.ChatMessages;

public class ChatMessageUserViewModel : ObservableObject, IChatMessage
{
    public ChatMessageUserViewModel(string message)
    {
        Timestamp = DateTimeOffset.Now;
        Message = message;
    }

    public string Message { get; }
    
    public DateTimeOffset Timestamp { get; }
}
