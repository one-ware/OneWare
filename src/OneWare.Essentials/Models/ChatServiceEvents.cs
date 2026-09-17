using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace OneWare.Essentials.Models;

public abstract class ChatEvent()
{
    /// <summary>
    /// Id of the sub-agent this event belongs to, or <c>null</c> when it comes from the main agent.
    /// The chat UI shows events of a sub-agent inside the corresponding
    /// <see cref="ChatSubAgentStartedEvent"/> block instead of the main conversation flow.
    /// </summary>
    public string? AgentId { get; init; }
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

public sealed class ChatToolExecutionStartEvent(string tool, string? toolCallId = null, bool isClientTool = false)
    : ChatEvent()
{
    public string Tool { get; } = tool;

    /// <summary>Id of the tool call, used to correlate with <see cref="ChatToolExecutionCompleteEvent"/>.</summary>
    public string? ToolCallId { get; } = toolCallId;

    /// <summary>
    /// True when the tool is executed by OneWare itself. Those tool calls are already reported
    /// through the AI function provider, so the chat UI only uses this event to attribute them to a
    /// sub-agent instead of rendering a second entry.
    /// </summary>
    public bool IsClientTool { get; } = isClientTool;

    /// <summary>Short description of what the tool was called with, when known.</summary>
    public string? Detail { get; init; }
}

/// <summary>
/// Completion of a tool call previously announced by <see cref="ChatToolExecutionStartEvent"/>.
/// </summary>
public sealed class ChatToolExecutionCompleteEvent(string toolCallId, bool success, string? output = null)
    : ChatEvent()
{
    public string ToolCallId { get; } = toolCallId;

    public bool Success { get; } = success;

    public string? Output { get; } = output;
}

/// <summary>
/// Raised when the agent delegated work to a sub-agent. The chat UI opens a collapsible block for
/// it; all following events carrying this <see cref="Id"/> as their <see cref="ChatEvent.AgentId"/>
/// belong inside that block.
/// </summary>
public sealed class ChatSubAgentStartedEvent(string id, string displayName)
    : ChatEvent()
{
    public string Id { get; } = id;

    public string DisplayName { get; } = displayName;

    /// <summary>What the sub-agent was created for, when the agent definition provides it.</summary>
    public string? Description { get; init; }

    /// <summary>Model the sub-agent runs with, when known.</summary>
    public string? Model { get; init; }

    /// <summary>True when the sub-agent runs in the background instead of blocking its parent.</summary>
    public bool IsBackground { get; init; }

    /// <summary>Id of the spawning sub-agent, for nested delegation. Null when the main agent spawned it.</summary>
    public string? ParentSubAgentId { get; init; }
}

/// <summary>
/// Raised when a sub-agent announced by <see cref="ChatSubAgentStartedEvent"/> finished, failed or
/// was cancelled.
/// </summary>
public sealed class ChatSubAgentCompletedEvent(string id, bool success)
    : ChatEvent()
{
    public string Id { get; } = id;

    public bool Success { get; } = success;

    /// <summary>Error message when the sub-agent failed.</summary>
    public string? Error { get; init; }

    /// <summary>True when the sub-agent was torn down instead of finishing its work.</summary>
    public bool Cancelled { get; init; }

    public TimeSpan? Duration { get; init; }

    public long? TotalTokens { get; init; }

    public long? TotalToolCalls { get; init; }
}

/// <summary>
/// Raised when the AI loaded a skill into its context.
/// </summary>
public sealed class ChatSkillLoadedEvent(string skillName, string content)
    : ChatEvent()
{
    public string SkillName { get; } = skillName;

    /// <summary>The skill instructions the AI received.</summary>
    public string Content { get; } = content;
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
