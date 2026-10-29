using System.Collections.Specialized;
using System.IO;
using Avalonia.Threading;
using GitHub.Copilot;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;

namespace OneWare.Copilot.Services;

/// <summary>
/// Keeps a chat in sync with the IDE: the skills and instruction files of every open project (the runtime only
/// discovers its working directory). MCP servers are handled in <c>CopilotChatService.Mcp.cs</c>.
/// </summary>
public abstract partial class CopilotChatServiceBase
{
    private string? _sessionWorkingDirectory;
    private string? _announcedWorkspaceKey;
    private CancellationTokenSource? _workspaceCts;
    private CancellationTokenSource? _projectsRefreshCts;
    private readonly SemaphoreSlim _workspaceGate = new(1, 1);
    private INotifyCollectionChanged? _observedProjects;

    private static ILogger Logger => ContainerLocator.Container.Resolve<ILogger>();

    private static async Task<IReadOnlyList<string>> GetOpenProjectRootsAsync()
    {
        var explorer = ContainerLocator.Container.Resolve<IProjectExplorerService>();

        List<string> Snapshot()
        {
            var roots = explorer.Projects.Select(x => x.RootFolderPath)
                .Where(x => !string.IsNullOrWhiteSpace(x) && Directory.Exists(x))
                .Select(Path.GetFullPath)
                .Distinct(CopilotSessionConfiguration.PathComparer)
                .ToList();

            // The active project goes first, matching the session's working directory.
            if (explorer.ActiveProject?.RootFolderPath is { } active &&
                roots.FindIndex(x => CopilotSessionConfiguration.PathComparer.Equals(x, Path.GetFullPath(active)))
                    is > 0 and var index)
            {
                var root = roots[index];
                roots.RemoveAt(index);
                roots.Insert(0, root);
            }

            return roots;
        }

        return Dispatcher.UIThread.CheckAccess() ? Snapshot() : await Dispatcher.UIThread.InvokeAsync(Snapshot);
    }

    private void SubscribeToProjectChanges()
    {
        if (_observedProjects != null) return;

        _observedProjects = ContainerLocator.Container.Resolve<IProjectExplorerService>().Projects;
        _observedProjects.CollectionChanged += OnProjectsChanged;
        SubscribeToConnectors();
    }

    private void UnsubscribeFromProjectChanges()
    {
        if (_observedProjects != null) _observedProjects.CollectionChanged -= OnProjectsChanged;
        _observedProjects = null;
        UnsubscribeFromConnectors();
        Interlocked.Exchange(ref _projectsRefreshCts, null)?.Cancel();
    }

    private void OnProjectsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        ScheduleWorkspaceSync(updateSkills: true);

    private void ScheduleWorkspaceSync(bool updateSkills)
    {
        var session = _session;
        if (session == null) return;

        // Opening a folder with several projects or editing a list raises a burst of changes; they are applied
        // together.
        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _projectsRefreshCts, cts)?.Cancel();
        _ = Task.Delay(TimeSpan.FromMilliseconds(500), cts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled) _ = StartWorkspaceSync(session, updateSkills);
        }, TaskScheduler.Default);
    }

    private Task StartWorkspaceSync(CopilotSession session, bool updateSkills)
    {
        var token = (_workspaceCts ??= new CancellationTokenSource()).Token;
        return SyncWorkspaceAsync(session, updateSkills, token);
    }

    private void ResetWorkspaceState()
    {
        Interlocked.Exchange(ref _workspaceCts, null)?.Cancel();
        Interlocked.Exchange(ref _projectsRefreshCts, null)?.Cancel();
        ResetMcpState();
        _announcedWorkspaceKey = null;
        _sessionWorkingDirectory = null;
    }

    private async Task SyncWorkspaceAsync(CopilotSession session, bool updateSkills, CancellationToken token)
    {
        try
        {
            await _workspaceGate.WaitAsync(token);
            try
            {
                if (!ReferenceEquals(_session, session) || _sessionWorkingDirectory is not { } workingDirectory)
                    return;

                if (updateSkills)
                {
                    var roots = await GetOpenProjectRootsAsync();
                    await session.Rpc.Options.UpdateAsync(
                        skillDirectories: BuildSessionSkillDirectories(workingDirectory, roots),
                        cancellationToken: token);
                    await session.Rpc.Skills.ReloadAsync(token);
                }
            }
            finally
            {
                _workspaceGate.Release();
            }
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
        {
            // The session was closed or replaced meanwhile.
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Failed to apply the open projects to the Copilot session.");
        }
    }

    /// <summary>
    /// Prepends the open projects and their instruction files whenever they differ from what this session was
    /// last told, so instructions of projects besides the working directory apply without bloating every turn.
    /// </summary>
    private async Task<string> ApplyWorkspaceContextAsync(string prompt)
    {
        if (_sessionWorkingDirectory is not { } workingDirectory) return prompt;

        try
        {
            var roots = await GetOpenProjectRootsAsync();
            string? key = null;
            var text = await Task.Run(() =>
                CopilotSessionConfiguration.BuildWorkspaceContext(roots, workingDirectory, out key));

            if (string.Equals(key, _announcedWorkspaceKey, StringComparison.Ordinal)) return prompt;
            _announcedWorkspaceKey = key;

            return text == null ? prompt : $"{text}\n\n{prompt}";
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Failed to describe the open projects to the Copilot session.");
            return prompt;
        }
    }
}
