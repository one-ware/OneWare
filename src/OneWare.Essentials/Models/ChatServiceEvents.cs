namespace OneWare.Essentials.Models;

public enum ChatServiceMessageType
{
    AssistantDelta,
    AssistantMessage,
    Error,
    Idle
}

public sealed class ChatServiceMessageEvent(ChatServiceMessageType type, string? content = null, string? messageId = null)
{
    public ChatServiceMessageType Type { get; } = type;
    public string? Content { get; } = content;
    public string? MessageId { get; } = messageId;
}

public sealed class ChatInitializationStatus(bool success)
{
    public bool Success { get; } = success;
    public bool NeedsAuthentication { get; init; }
}

public sealed class ChatServiceStatusEvent(bool isConnected, string statusText)
{
    public bool IsConnected { get; } = isConnected;
    public string StatusText { get; } = statusText;
}
