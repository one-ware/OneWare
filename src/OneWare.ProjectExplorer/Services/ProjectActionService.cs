using Microsoft.Extensions.Logging;
using OneWare.Essentials.Models;
using OneWare.Essentials.ProjectActions;
using OneWare.Essentials.Services;

namespace OneWare.ProjectExplorer.Services;

public class ProjectActionService(
    ICompositeServiceProvider serviceProvider,
    IProjectExplorerService projectExplorerService,
    ILogger logger) : IProjectActionService
{
    private readonly Dictionary<string, IReadOnlyList<BuildArtifact>> _artifacts =
        new(StringComparer.OrdinalIgnoreCase);

    // Marks calls made from inside a running action (e.g. Debug triggering Build), so they join the
    // outer action instead of being rejected as a second one.
    private readonly AsyncLocal<bool> _insideAction = new();

    private readonly List<IProjectActionProvider> _providers = [];

    private CancellationTokenSource? _cancellation;

    public IReadOnlyList<IProjectActionProvider> Providers => _providers;

    public bool IsBusy => CurrentAction != null;

    public ProjectActionRequest? CurrentAction { get; private set; }

    public event EventHandler? StateChanged;

    public event EventHandler<ProjectActionCompletedEventArgs>? ActionCompleted;

    public void RegisterProvider<T>() where T : IProjectActionProvider
    {
        RegisterProvider(serviceProvider.Resolve<T>());
    }

    public void RegisterProvider(IProjectActionProvider provider)
    {
        if (_providers.Any(x => x.Id == provider.Id))
        {
            logger.Warning($"A project action provider with id '{provider.Id}' is already registered - ignoring.");
            return;
        }

        _providers.Add(provider);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public IProjectActionProvider? GetProvider(IProjectRoot project, ProjectActionKind kind)
    {
        return _providers
            .Where(x => SupportsSafely(x, project, kind))
            .OrderByDescending(x => x.Priority)
            .FirstOrDefault();
    }

    public bool CanExecute(ProjectActionKind kind, IProjectRoot? project = null)
    {
        project ??= projectExplorerService.ActiveProject;
        return project != null && GetProvider(project, kind) != null;
    }

    public async Task<ProjectActionResult> ExecuteAsync(ProjectActionKind kind, IProjectRoot? project = null)
    {
        project ??= projectExplorerService.ActiveProject;

        if (project == null)
        {
            logger.Warning("No active project.");
            return ProjectActionResult.Failed("No active project.");
        }

        if (GetProvider(project, kind) is not { } provider)
        {
            logger.Warning($"{kind} is not available for project '{project.Header}'.");
            return ProjectActionResult.Failed($"{kind} is not available for this project.");
        }

        var nested = _insideAction.Value;

        if (!nested && IsBusy)
        {
            logger.Warning($"Cannot start {kind}: {CurrentAction!.Kind} is still running.");
            return ProjectActionResult.Failed($"{CurrentAction.Kind} is still running.");
        }

        if (!nested) _cancellation = new CancellationTokenSource();

        var request = new ProjectActionRequest(project, kind, _cancellation?.Token ?? CancellationToken.None);

        if (!nested)
        {
            CurrentAction = request;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        ProjectActionResult result;
        try
        {
            _insideAction.Value = true;

            if (kind is ProjectActionKind.Build or ProjectActionKind.Run or ProjectActionKind.Debug)
                await projectExplorerService.SaveOpenFilesForProjectAsync(project);

            result = await provider.ExecuteAsync(request);
        }
        catch (OperationCanceledException)
        {
            result = ProjectActionResult.Failed($"{kind} was cancelled.");
        }
        catch (Exception e)
        {
            logger.Error($"{kind} failed in '{provider.Id}': {e.Message}", e);
            result = ProjectActionResult.Failed(e.Message);
        }
        finally
        {
            _insideAction.Value = nested;

            if (!nested)
            {
                CurrentAction = null;
                _cancellation?.Dispose();
                _cancellation = null;
            }
        }

        if (kind == ProjectActionKind.Build && result.Success)
            _artifacts[project.FullPath] = result.Artifacts ?? [];
        else if (kind == ProjectActionKind.Clean && result.Success)
            _artifacts.Remove(project.FullPath);

        ActionCompleted?.Invoke(this, new ProjectActionCompletedEventArgs(request, result));
        if (!nested) StateChanged?.Invoke(this, EventArgs.Empty);

        return result;
    }

    public void Cancel()
    {
        _cancellation?.Cancel();
    }

    public IReadOnlyList<BuildArtifact> GetArtifacts(IProjectRoot project)
    {
        return _artifacts.TryGetValue(project.FullPath, out var artifacts) ? artifacts : [];
    }

    private bool SupportsSafely(IProjectActionProvider provider, IProjectRoot project, ProjectActionKind kind)
    {
        try
        {
            return provider.Supports(project, kind);
        }
        catch (Exception e)
        {
            logger.Error($"Project action provider '{provider.Id}' failed in Supports: {e.Message}", e);
            return false;
        }
    }
}
