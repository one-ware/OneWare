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

    /// <summary>Name of the model that wrote this message, when the chat service reports it.</summary>
    [DataMember]
    public string? Model
    {
        get;
        set
        {
            if (SetProperty(ref field, value)) OnPropertyChanged(nameof(HasModel));
        }
    }

    /// <summary>
    /// Whether the model is shown beneath the message. Only set on the message that ended a turn,
    /// so the attribution reads as "this answer was completed by X" instead of repeating for every
    /// intermediate message.
    /// </summary>
    [DataMember]
    public bool ShowModel
    {
        get;
        set
        {
            if (SetProperty(ref field, value)) OnPropertyChanged(nameof(HasModel));
        }
    }

    public bool HasModel => ShowModel && !string.IsNullOrWhiteSpace(Model);

    public double EstimateHeight(double width) => ChatHeightEstimation.EstimateMarkdown(Content, width);
}
