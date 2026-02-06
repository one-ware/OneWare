using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace OneWare.Essentials.Models;

public abstract class ChatEvent()
{

}

public sealed class ChatMessageDeltaEvent(string content, string? messageId = null)
    : ChatEvent()
{
    public string Content { get; } = content;
    
    public string? MessageId { get; } = messageId;
}

public sealed class ChatMessageEvent(string content, string? messageId = null)
    : ChatEvent()
{
    public string Content { get; } = content;
    
    public string? MessageId { get; } = messageId;
}

public sealed class ChatReasoningDeltaEvent(string content, string? reasoningId = null)
    : ChatEvent()
{
    public string Content { get; } = content;
    
    public string? ReasoningId { get; } = reasoningId;
}

public sealed class ChatReasoningEvent(string content, string? reasoningId = null)
    : ChatEvent()
{
    public string Content { get; } = content;
    
    public string? ReasoningId { get; } = reasoningId;
}

public sealed class ChatUserMessageEvent(string content)
    : ChatEvent()
{
    public string Content { get; } = content;
}

public sealed class ChatToolExecutionStartEvent(string tool)
    : ChatEvent()
{
    public string Tool { get; } = tool;
}

public sealed class ChatErrorEvent(string message)
    : ChatEvent()
{
    public string? Message { get; } = message;
}

public sealed class ChatButtonEvent(string message, string buttonText, IRelayCommand<Control?> command)
    : ChatEvent()
{
    public string? Message { get; } = message;
    
    public string ButtonText { get; } = buttonText;

    public IRelayCommand<Control?> OnClickCommand { get; init; } = command;
}

public sealed class ChatIdleEvent()
    : ChatEvent()
{
}

public sealed class StatusEvent(bool isConnected, string statusText)
{
    public bool IsConnected { get; } = isConnected;
    public string StatusText { get; } = statusText;
}
