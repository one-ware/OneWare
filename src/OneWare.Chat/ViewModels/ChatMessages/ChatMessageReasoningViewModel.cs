using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Controls;

namespace OneWare.Chat.ViewModels.ChatMessages;

public class ChatMessageReasoningViewModel : ObservableObject, IChatMessage, IEstimatedHeightItem
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

    public double EstimateHeight(double width)
    {
        const double header = 36;
        if (!IsStreaming)
            return header;
        return header + System.Math.Min(200, ChatHeightEstimation.EstimateMarkdown(Content, width)) + 8;
    }
}
