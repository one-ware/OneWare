using OneWare.Essentials.Models;

namespace OneWare.Essentials.ProjectActions;

/// <summary>
/// Describes one execution of a project action. A record so new information can be added later without
/// changing the <see cref="IProjectActionProvider" /> signature that plugins implement.
/// </summary>
/// <param name="Project">The project the action runs on.</param>
/// <param name="Kind">The requested action.</param>
/// <param name="CancellationToken">Cancelled when the user stops the action.</param>
public sealed record ProjectActionRequest(
    IProjectRoot Project,
    ProjectActionKind Kind,
    CancellationToken CancellationToken);
