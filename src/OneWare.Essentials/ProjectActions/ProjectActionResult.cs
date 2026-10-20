namespace OneWare.Essentials.ProjectActions;

/// <summary>
/// The outcome of a project action.
/// </summary>
/// <param name="Success">Whether the action completed successfully.</param>
/// <param name="Artifacts">Files produced by the action (usually only set by <see cref="ProjectActionKind.Build" />).</param>
/// <param name="Message">Optional short message, e.g. why the action failed.</param>
public sealed record ProjectActionResult(
    bool Success,
    IReadOnlyList<BuildArtifact>? Artifacts = null,
    string? Message = null)
{
    public static ProjectActionResult Succeeded(IReadOnlyList<BuildArtifact>? artifacts = null)
    {
        return new ProjectActionResult(true, artifacts);
    }

    public static ProjectActionResult Failed(string? message = null)
    {
        return new ProjectActionResult(false, null, message);
    }
}
