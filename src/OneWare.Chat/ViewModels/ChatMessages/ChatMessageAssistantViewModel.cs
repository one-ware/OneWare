using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Chat.ViewModels.ChatMessages;

public class ChatMessageAssistantViewModel : ObservableObject, IChatMessage
{
    public ChatMessageAssistantViewModel(string? messageId = null)
    {
        MessageId = messageId;
        Timestamp = DateTimeOffset.Now;
    }
    
    public string? MessageId { get; }
    
    public DateTimeOffset Timestamp { get; }
    
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
