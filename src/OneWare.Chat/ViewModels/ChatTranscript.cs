using System.Text.RegularExpressions;
using OneWare.Chat.ViewModels.ChatMessages;

namespace OneWare.Chat.ViewModels;

/// <summary>Converts chat messages to and from the form they are stored in the chat history.</summary>
internal static class ChatTranscript
{
    public static ChatMessageState? BuildMessageState(IChatMessage message)
    {
        switch (message)
        {
            case ChatMessageUserViewModel user:
                return new ChatMessageState(ChatMessageKind.User)
                {
                    Message = user.Message
                };
            case ChatMessageAssistantViewModel assistant:
                return new ChatMessageState(ChatMessageKind.Assistant)
                {
                    Content = assistant.Content,
                    Model = assistant.Model,
                    ShowModel = assistant.ShowModel
                };
            case ChatMessageReasoningViewModel reasoning:
                return new ChatMessageState(ChatMessageKind.Reasoning)
                {
                    Content = reasoning.Content
                };
            case ChatMessageToolViewModel tool:
                return new ChatMessageState(ChatMessageKind.Tool)
                {
                    Id = tool.Id,
                    ToolName = tool.ToolName,
                    ToolOutput = tool.ToolOutput,
                    IsSuccessful = tool.IsSuccessful
                };
            case ChatMessageErrorViewModel:
                // Error chat messages are intentionally not serialized.
                return null;
            case ChatMessageSkillViewModel skill:
                return new ChatMessageState(ChatMessageKind.Skill)
                {
                    SkillName = skill.SkillName,
                    Content = skill.Content
                };
            case ChatMessageSubAgentViewModel subAgent:
                return new ChatMessageState(ChatMessageKind.SubAgent)
                {
                    Id = subAgent.Id,
                    Message = subAgent.DisplayName,
                    Content = subAgent.Description,
                    Instructions = subAgent.Instructions,
                    Model = subAgent.Model,
                    ToolOutput = subAgent.StatusText,
                    IsSuccessful = subAgent.IsSuccessful,
                    Children = subAgent.Items.Select(BuildMessageState).OfType<ChatMessageState>().ToList()
                };
            default:
                return null;
        }
    }

    public static bool TryCreateMessage(ChatMessageState state, out IChatMessage message)
    {
        switch (state.Kind)
        {
            case ChatMessageKind.User:
                message = new ChatMessageUserViewModel(state.Message ?? string.Empty);
                return true;
            case ChatMessageKind.Assistant:
                message = new ChatMessageAssistantViewModel
                {
                    Content = state.Content ?? string.Empty,
                    IsStreaming = false,
                    Model = state.Model,
                    ShowModel = state.ShowModel
                };
                return true;
            case ChatMessageKind.Reasoning:
                message = new ChatMessageReasoningViewModel
                {
                    Content = state.Content ?? string.Empty,
                    IsStreaming = false
                };
                return true;
            case ChatMessageKind.Tool:
                if (string.IsNullOrWhiteSpace(state.ToolName))
                {
                    message = null!;
                    return false;
                }

                message = new ChatMessageToolViewModel(state.Id ?? Guid.NewGuid().ToString("N"), state.ToolName)
                {
                    ToolOutput = state.ToolOutput,
                    IsSuccessful = state.IsSuccessful,
                    IsToolRunning = false
                };
                return true;
            case ChatMessageKind.Skill:
                if (string.IsNullOrWhiteSpace(state.SkillName))
                {
                    message = null!;
                    return false;
                }

                message = new ChatMessageSkillViewModel(state.SkillName, state.Content ?? string.Empty);
                return true;
            case ChatMessageKind.SubAgent:
                if (string.IsNullOrWhiteSpace(state.Message))
                {
                    message = null!;
                    return false;
                }

                var restored = new ChatMessageSubAgentViewModel(state.Id ?? Guid.NewGuid().ToString("N"),
                    state.Message)
                {
                    Description = state.Content,
                    Instructions = state.Instructions,
                    // Sessions written before the dedicated field kept the model in ToolName.
                    Model = state.Model ?? state.ToolName,
                    IsRunning = false,
                    IsExpanded = false,
                    IsSuccessful = state.IsSuccessful,
                    StatusText = state.ToolOutput ?? string.Empty
                };

                foreach (var childState in state.Children)
                {
                    if (TryCreateMessage(childState, out var child))
                        restored.Items.Add(child);
                }

                message = restored;
                return true;
            default:
                message = null!;
                return false;
        }
    }

    public static List<ChatMessageState> BuildStates(IEnumerable<IChatMessage> messages) =>
        messages.Select(BuildMessageState).OfType<ChatMessageState>().ToList();

    public static string BuildChatName(IReadOnlyCollection<ChatMessageState> messages) =>
        BuildChatName(messages.FirstOrDefault(x => x.Kind == ChatMessageKind.User)?.Message);

    /// <summary>Name of a chat that starts with <paramref name="firstUserMessage" />.</summary>
    public static string BuildChatName(string? firstUserMessage)
    {
        if (string.IsNullOrWhiteSpace(firstUserMessage)) return "Chat";

        var normalized = Regex.Replace(firstUserMessage.Trim(), "\\s+", " ");
        return normalized.Length > 64 ? normalized[..64].Trim() : normalized;
    }
}

internal sealed class ChatMessageState
{
    public ChatMessageState()
    {
    }

    public ChatMessageState(ChatMessageKind kind)
    {
        Kind = kind;
    }

    public ChatMessageKind Kind { get; set; }
    public string? Message { get; set; }
    public string? Content { get; set; }
    public string? Id { get; set; }
    public string? ToolName { get; set; }
    public string? ToolOutput { get; set; }
    public string? SkillName { get; set; }

    /// <summary>Instructions a sub-agent was started with.</summary>
    public string? Instructions { get; set; }

    /// <summary>Model that produced the message, or that a sub-agent ran with.</summary>
    public string? Model { get; set; }

    /// <summary>Whether the message is the one that ended its turn and therefore names its model.</summary>
    public bool ShowModel { get; set; }
    public bool IsSuccessful { get; set; }

    /// <summary>Messages nested inside a sub-agent block.</summary>
    public List<ChatMessageState> Children { get; set; } = [];
}

internal enum ChatMessageKind
{
    User,
    Assistant,
    Reasoning,
    Tool,
    Skill,
    SubAgent
}
