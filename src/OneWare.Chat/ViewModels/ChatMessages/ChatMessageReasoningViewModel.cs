using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Chat.ViewModels.ChatMessages;

public class ChatMessageReasoningViewModel : ObservableObject, IChatMessage
{
    public ChatMessageReasoningViewModel(string? reasoningId = null)
    {
        ReasoningId = reasoningId;
        Timestamp = DateTimeOffset.Now;
    }
    
    public string? ReasoningId { get; }
    
    public DateTimeOffset Timestamp { get; }
    
    [DataMember]
    public string Content
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public bool IsStreaming
    {
        get;
        set => SetProperty(ref field, value);
    }
}
