namespace OneWare.ChatBot.Services;

public enum ChatServiceMessageType
{
    AssistantDelta,
    AssistantMessage,
    Error,
    Idle
}

public sealed class ChatServiceMessageEvent(ChatServiceMessageType type, string? content = null)
{
    public ChatServiceMessageType Type { get; } = type;
    public string? Content { get; } = content;
}

public sealed class ChatServiceStatusEvent(bool isConnected, string statusText)
{
    public bool IsConnected { get; } = isConnected;
    public string StatusText { get; } = statusText;
}
