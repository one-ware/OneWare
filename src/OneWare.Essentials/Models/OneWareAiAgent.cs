namespace OneWare.Essentials.Models;

/// <summary>
/// A custom AI agent contributed by a module or plugin. Agents bundle domain knowledge
/// (<see cref="Instructions"/>) with an optional tool/skill selection, so the main chat agent can
/// delegate specialized work to them.
/// </summary>
/// <example>
/// <code>
/// aiFunctionProvider.RegisterAgent(new OneWareAiAgent
/// {
///     Name = "oneai-dataset",
///     DisplayName = "OneAI Dataset",
///     Description = "Use for creating or editing OneAI datasets and .oneai files.",
///     Instructions = "You are an expert for OneAI datasets. ..."
/// });
/// </code>
/// </example>
public sealed class OneWareAiAgent
{
    /// <summary>
    /// Unique agent identifier. Use a lowercase, hyphenated name (e.g. <c>oneai-dataset</c>);
    /// registering the same name twice replaces the previous agent.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Explains to the main agent when it should delegate to this agent. Keep it specific — this is
    /// the only information the model uses to decide whether the agent is relevant.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// System prompt of the agent: the domain knowledge, rules and workflow it should follow.
    /// </summary>
    public required string Instructions { get; init; }

    /// <summary>
    /// Optional human-readable name shown in the UI. Defaults to <see cref="Name"/>.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Names of the tools the agent may use. <see langword="null"/> grants the default tool set.
    /// </summary>
    public IReadOnlyList<string>? Tools { get; init; }

    /// <summary>
    /// Names of skills (see <see cref="OneWareAiSkill.Name"/>) the agent may load.
    /// </summary>
    public IReadOnlyList<string>? Skills { get; init; }

    /// <summary>
    /// Optional model override (e.g. a cheaper model for a narrow agent). Uses the session model
    /// when <see langword="null"/>.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Optional reasoning effort override ("low", "medium", "high", "xhigh").
    /// </summary>
    public string? ReasoningEffort { get; init; }

    /// <summary>
    /// Whether the main agent may delegate to this agent on its own. Defaults to
    /// <see langword="true"/>; set to <see langword="false"/> for agents that should only run when
    /// explicitly requested.
    /// </summary>
    public bool Infer { get; init; } = true;
}
