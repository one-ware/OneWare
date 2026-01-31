using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Chat.ViewModels.ChatMessages;

public class ChatMessageUserViewModel : ObservableObject, IChatMessage
{
    public ChatMessageUserViewModel(string message)
    {
        Timestamp = DateTimeOffset.Now;
        Message = message;
    }
    
    [DataMember]
    public string Message { get; }
    
    public DateTimeOffset Timestamp { get; }
}
