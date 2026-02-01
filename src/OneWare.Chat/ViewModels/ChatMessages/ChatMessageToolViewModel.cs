using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Chat.ViewModels.ChatMessages;

public class ChatMessageToolViewModel : ObservableObject, IChatMessage
{
    public ChatMessageToolViewModel(string id, string toolName)
    {
        Timestamp = DateTimeOffset.Now;
        Id = id;
        ToolName = toolName;
    }
    
    public string Id { get; init; }
    
    [DataMember]
    public string ToolName { get; }
    
    [DataMember]
    public string? ToolOutput { get; set; }
    
    public DateTimeOffset Timestamp { get; }

    public bool IsToolRunning
    {
        get;
        set => SetProperty(ref field, value);
    }
    
    [DataMember]
    public bool IsSuccessful
    {
        get;
        set => SetProperty(ref field, value);
    }
}
