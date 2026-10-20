using OneWare.Essentials.Models;

namespace OneWare.Essentials.ProjectActions;

public sealed class ProjectActionCompletedEventArgs(ProjectActionRequest request, ProjectActionResult result)
    : EventArgs
{
    public ProjectActionRequest Request { get; } = request;
    public ProjectActionResult Result { get; } = result;
}

/// <summary>
/// Runs Build / Clean / Run / Program / Debug on projects by dispatching to the registered
/// <see cref="IProjectActionProvider" />s. The shared toolbar and menus use it for the active project.
/// </summary>
public interface IProjectActionService
{
    /// <summary>
    /// All registered providers.
    /// </summary>
    IReadOnlyList<IProjectActionProvider> Providers { get; }

    /// <summary>
    /// True while an action is running.
    /// </summary>
    bool IsBusy { get; }

    /// <summary>
    /// The outermost running action, or null.
    /// </summary>
    ProjectActionRequest? CurrentAction { get; }

    /// <summary>
    /// Raised when providers are registered or an action starts or ends.
    /// </summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// Raised after an action finished (successfully or not).
    /// </summary>
    event EventHandler<ProjectActionCompletedEventArgs>? ActionCompleted;

    /// <summary>
    /// Registers a provider resolved from the container. Duplicate ids are ignored.
    /// </summary>
    void RegisterProvider<T>() where T : IProjectActionProvider;

    /// <summary>
    /// Registers a provider instance. Duplicate ids are ignored.
    /// </summary>
    void RegisterProvider(IProjectActionProvider provider);

    /// <summary>
    /// The provider that would run <paramref name="kind" /> for <paramref name="project" />, or null.
    /// </summary>
    IProjectActionProvider? GetProvider(IProjectRoot project, ProjectActionKind kind);

    /// <summary>
    /// Whether <paramref name="kind" /> can run for <paramref name="project" /> (the active project if null).
    /// </summary>
    bool CanExecute(ProjectActionKind kind, IProjectRoot? project = null);

    /// <summary>
    /// Runs <paramref name="kind" /> for <paramref name="project" /> (the active project if null).
    /// Only one top-level action runs at a time; providers may call this again from inside their own action
    /// (e.g. Debug triggering Build), which then runs as part of the outer action.
    /// </summary>
    Task<ProjectActionResult> ExecuteAsync(ProjectActionKind kind, IProjectRoot? project = null);

    /// <summary>
    /// Requests cancellation of the running action.
    /// </summary>
    void Cancel();

    /// <summary>
    /// Files produced by the last successful build of <paramref name="project" />.
    /// </summary>
    IReadOnlyList<BuildArtifact> GetArtifacts(IProjectRoot project);
}
