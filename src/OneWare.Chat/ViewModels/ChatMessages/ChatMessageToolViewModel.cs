using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Controls;

namespace OneWare.Chat.ViewModels.ChatMessages;

public class ChatMessageToolViewModel : ObservableObject, IChatMessage, IEstimatedHeightItem
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
    public string? ToolOutput
    {
        get;
        set => SetProperty(ref field, value);
    }
    
    public DateTimeOffset Timestamp { get; }

    public bool IsToolRunning
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>Cancels this tool invocation while it is running.</summary>
    public IRelayCommand? StopCommand
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

    public double EstimateHeight(double width)
    {
        const double header = 36;
        if (!IsToolRunning)
            return header;
        return header + System.Math.Min(200, ChatHeightEstimation.EstimateMarkdown(ToolOutput, width)) + 8;
    }
}
