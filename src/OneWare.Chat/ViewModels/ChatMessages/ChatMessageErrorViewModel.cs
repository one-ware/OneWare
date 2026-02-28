using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Chat.ViewModels.ChatMessages;

public class ChatMessageErrorViewModel : ObservableObject, IChatMessage
{
    public ChatMessageErrorViewModel(string message)
    {
        Timestamp = DateTimeOffset.Now;
        Message = message;
    }

    public string Message { get; }

    public DateTimeOffset Timestamp { get; }
}
