namespace OneWare.Essentials.Models;

public enum ProjectExplorerTagKind
{
    Neutral,
    Accent,
    Success,
    Warning,
    Error
}

/// <summary>
///     Small pill shown after the name of a node in the project explorer, e.g. "Top" or "Testbench".
///     The kind picks the badge color.
/// </summary>
public sealed record ProjectExplorerTag(
    string Text,
    ProjectExplorerTagKind Kind = ProjectExplorerTagKind.Neutral,
    string? ToolTip = null);
