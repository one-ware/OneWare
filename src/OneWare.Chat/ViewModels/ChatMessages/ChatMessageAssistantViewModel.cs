using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Controls;

namespace OneWare.Chat.ViewModels.ChatMessages;

public class ChatMessageAssistantViewModel : ObservableObject, IChatMessage, IEstimatedHeightItem
{
    public ChatMessageAssistantViewModel(string? messageId = null)
    {
        MessageId = messageId;
        Timestamp = DateTimeOffset.Now;
    }
    
    public string? MessageId { get; }
    
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

    public double EstimateHeight(double width) => ChatHeightEstimation.EstimateMarkdown(Content, width);
}
