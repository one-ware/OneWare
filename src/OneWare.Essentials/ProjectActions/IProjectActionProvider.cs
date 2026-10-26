using OneWare.Essentials.Models;

namespace OneWare.Essentials.ProjectActions;

/// <summary>
/// Implements Build / Clean / Run / Program / Debug for one or more project types.
/// Register it with <see cref="IProjectActionService.RegisterProvider{T}" />.
/// </summary>
public interface IProjectActionProvider
{
    /// <summary>
    /// Unique id of the provider.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// When several providers support the same project and action, the one with the highest priority runs.
    /// Generic fallbacks use a negative priority.
    /// </summary>
    int Priority => 0;

    /// <summary>
    /// Whether the shared Build / Run / Program / Debug toolbar shows buttons for this provider.
    /// Project types with their own toolbar (e.g. FPGA projects) return false to avoid duplicate buttons;
    /// their actions still run through <see cref="IProjectActionService" />.
    /// </summary>
    bool UsesSharedToolBar => true;

    /// <summary>
    /// Whether this provider can run <paramref name="kind" /> for <paramref name="project" />.
    /// Called often (e.g. to update toolbar state), so it must be cheap and must not throw.
    /// </summary>
    bool Supports(IProjectRoot project, ProjectActionKind kind);

    /// <summary>
    /// Runs the action. Report progress and errors through the output and error list; the returned result
    /// only states whether it succeeded and which files were produced.
    /// </summary>
    Task<ProjectActionResult> ExecuteAsync(ProjectActionRequest request);
}
