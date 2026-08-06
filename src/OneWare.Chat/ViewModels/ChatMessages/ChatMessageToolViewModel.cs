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

    /// <summary>
    /// Live view model shown inside the tool box instead of the plain output text, e.g. the
    /// mini terminal of a terminal command. Not persisted: restored sessions fall back to
    /// <see cref="ToolOutput"/>.
    /// </summary>
    public object? EmbeddedContent
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;
            OnPropertyChanged(nameof(HasEmbeddedContent));
            OnPropertyChanged(nameof(IsExpandedByDefault));
        }
    }

    public bool HasEmbeddedContent => EmbeddedContent != null;

    /// <summary>
    /// Tool boxes collapse once the tool finished, but a mini terminal is the point of the
    /// message and stays visible.
    /// </summary>
    public bool IsExpandedByDefault => IsToolRunning || HasEmbeddedContent;
    
    public DateTimeOffset Timestamp { get; }

    public bool IsToolRunning
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;
            OnPropertyChanged(nameof(IsExpandedByDefault));
        }
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
        // Header, terminal chrome and the fixed 180px terminal body of EmbeddedTerminalView.
        if (HasEmbeddedContent) return header + 218;
        if (!IsToolRunning)
            return header;
        return header + System.Math.Min(200, ChatHeightEstimation.EstimateMarkdown(ToolOutput, width)) + 8;
    }
}
