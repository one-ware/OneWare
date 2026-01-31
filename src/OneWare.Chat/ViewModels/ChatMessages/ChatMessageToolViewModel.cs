using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Chat.ViewModels.ChatMessages;

public class ChatMessageToolViewModel : ObservableObject, IChatMessage
{
    public ChatMessageToolViewModel(string toolMessage)
    {
        Timestamp = DateTimeOffset.Now;
        ToolMessage = toolMessage;
    }
    
    [DataMember]
    public string ToolMessage { get; }
    
    [DataMember]
    public string? ToolFinishMessage { get; set; }
    
    public DateTimeOffset Timestamp { get; }

    public bool IsToolRunning
    {
        get;
        set => SetProperty(ref field, value);
    }
}
