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

public sealed class ChatPermissionRequestEvent(
    string message,
    string allowButtonText,
    string denyButtonText,
    IRelayCommand<Control?> allowCommand,
    IRelayCommand<Control?> denyCommand,
    string? allowForSessionButtonText = null,
    IRelayCommand<Control?>? allowForSessionCommand = null)
    : ChatEvent()
{
    public string Message { get; } = message;

    public string AllowButtonText { get; } = allowButtonText;

    public string DenyButtonText { get; } = denyButtonText;

    public IRelayCommand<Control?> AllowCommand { get; } = allowCommand;

    public IRelayCommand<Control?> DenyCommand { get; } = denyCommand;

    public string? AllowForSessionButtonText { get; } = allowForSessionButtonText;

    public IRelayCommand<Control?>? AllowForSessionCommand { get; } = allowForSessionCommand;
}

public sealed class ChatIdleEvent()
    : ChatEvent()
{
}

/// <summary>
/// Emitted when the agent asks the user a question mid-turn (free-form and/or multiple choice).
/// The chat UI must show an interactive prompt and invoke <see cref="SubmitCommand"/> with the
/// user's answer; the service blocks the agent callback until then.
/// </summary>
public sealed class ChatUserInputRequestEvent(
    string question,
    IReadOnlyList<string> choices,
    bool allowFreeform,
    IRelayCommand<string?> submitCommand)
    : ChatEvent()
{
    public string Question { get; } = question;

    public IReadOnlyList<string> Choices { get; } = choices;

    public bool AllowFreeform { get; } = allowFreeform;

    /// <summary>Invoked with the chosen or typed answer string.</summary>
    public IRelayCommand<string?> SubmitCommand { get; } = submitCommand;
}

/// <summary>
/// Signals the chat UI to clear all current messages and start fresh.
/// Emitted when the service initiates a new session autonomously (e.g. a remote session),
/// without going through the normal <see cref="IChatService.NewChatAsync"/> path.
/// </summary>
public sealed class ChatClearMessagesEvent() : ChatEvent()
{
}

public sealed class StatusEvent(bool isConnected, string statusText)
{
    public bool IsConnected { get; } = isConnected;
    public string StatusText { get; } = statusText;
}
