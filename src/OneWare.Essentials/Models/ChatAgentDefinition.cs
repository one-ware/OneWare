namespace OneWare.Essentials.Models;

/// <summary>
/// The conversational mode a chat agent runs its turns in.
/// </summary>
public enum ChatAgentTurnMode
{
    /// <summary>The agent answers and acts interactively.</summary>
    Interactive,

    /// <summary>The agent researches and proposes a plan before changing anything.</summary>
    Plan
}

/// <summary>
/// Ids of the chat agents that ship with OneWare.
/// </summary>
public static class BuiltInChatAgents
{
    /// <summary>Full access agent that carries work out.</summary>
    public const string Agent = "agent";

    /// <summary>Read-only agent that works out a plan first.</summary>
    public const string Plan = "plan";

    /// <summary>Read-only agent that only answers questions.</summary>
    public const string Ask = "ask";
}

/// <summary>
/// A user-selectable chat agent ("chat mode"): the personality, tool budget and turn mode the chat
/// uses for the messages that are sent while it is selected.
/// </summary>
/// <remarks>
/// Built-in agents (<c>Agent</c>, <c>Plan</c>, <c>Ask</c>) ship with OneWare. Additional agents come
/// from modules via <see cref="Services.IChatAgentService.RegisterAgent"/> or from markdown files in
/// <c>.github/agents</c> of the active project.
/// </remarks>
public sealed class ChatAgentDefinition
{
    /// <summary>
    /// Unique identifier. Use a lowercase, hyphenated name (e.g. <c>code-review</c>); registering the
    /// same id twice replaces the previous agent.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>Name shown in the agent selector.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Short explanation shown as tooltip in the agent selector.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Additional instructions applied to every message sent while this agent is selected.
    /// </summary>
    public string? Instructions { get; init; }

    /// <summary>The turn mode the chat backend should use.</summary>
    public ChatAgentTurnMode TurnMode { get; init; } = ChatAgentTurnMode.Interactive;

    /// <summary>
    /// When <see langword="true"/>, tools that change the workspace or the IDE are blocked, so the
    /// agent can only research and answer.
    /// </summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// Names of the tools the agent may use. <see langword="null"/> grants the full tool set.
    /// </summary>
    public IReadOnlyList<string>? Tools { get; init; }

    /// <summary>Optional model override. Uses the chat's selected model when <see langword="null"/>.</summary>
    public string? Model { get; init; }

    /// <summary>Optional reasoning effort override ("low", "medium", "high", "xhigh").</summary>
    public string? ReasoningEffort { get; init; }

    /// <summary>Whether the agent ships with OneWare and cannot be removed.</summary>
    public bool IsBuiltIn { get; init; }

    /// <summary>Markdown file the agent was loaded from, if it is file-based.</summary>
    public string? SourcePath { get; init; }

    public override string ToString() => DisplayName;
}
