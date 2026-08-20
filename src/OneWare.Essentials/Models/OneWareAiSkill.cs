namespace OneWare.Essentials.Models;

/// <summary>
/// A skill contributed by a module or plugin: a self-contained instruction document the AI loads on
/// demand when the task matches its <see cref="Description"/>.
/// </summary>
/// <remarks>
/// Use this for skills defined in code. Skills that ship additional resources (scripts, templates,
/// reference documents) should be registered with
/// <see cref="Services.IAiFunctionProvider.RegisterSkillDirectory"/> instead.
/// </remarks>
public sealed class OneWareAiSkill
{
    /// <summary>
    /// Unique skill identifier. Use a lowercase, hyphenated name (e.g. <c>oneai-dataset-format</c>);
    /// registering the same name twice replaces the previous skill.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Explains when the skill should be loaded. This is the only part of the skill that is always
    /// in context, so it must state the trigger conditions precisely.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Markdown body of the skill.
    /// </summary>
    public required string Instructions { get; init; }
}
