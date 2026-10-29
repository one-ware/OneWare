using System.Collections.Specialized;
using System.IO;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Extensions.Logging;
using OneWare.Copilot.Connectors;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Copilot.Services;

/// <summary>
/// Keeps a chat in sync with the IDE: the skills and instruction files of every open project (the runtime only
/// discovers its working directory) and the connectors (IDE-wide MCP servers) edited in the settings.
/// </summary>
public abstract partial class CopilotChatServiceBase
{
    private string? _sessionWorkingDirectory;
    private string? _announcedWorkspaceKey;
    private CancellationTokenSource? _workspaceCts;
    private CancellationTokenSource? _projectsRefreshCts;
    private readonly SemaphoreSlim _workspaceGate = new(1, 1);
    private INotifyCollectionChanged? _observedProjects;
    private CopilotConnectorService? _observedConnectors;

    // MCP problems already shown in this session (sign-in offers, failures), so they are not repeated.
    private readonly HashSet<string> _reportedMcpProblems = new(StringComparer.Ordinal);

    // Servers the user started signing in to from the chat, so their reconnection is confirmed in the chat.
    private readonly HashSet<string> _mcpSignInsInProgress = new(StringComparer.Ordinal);

    // Connectors this session runs with values stored in OneWare filled in, by name. The runtime only knows their
    // definitions with ${VAR} references, so the session gets them as its own servers.
    private readonly Dictionary<string, McpConnector> _sessionConnectors = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _connectorsGate = new(1, 1);
    private volatile bool _applyingConnectors;

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

        _observedConnectors = ContainerLocator.Container.Resolve<CopilotConnectorService>();
        _observedConnectors.ConnectorsChanged += OnConnectorsChanged;
        _observedConnectors.SignedIn += OnConnectorSignedIn;
        _observedConnectors.ValuesChanged += OnConnectorsChanged;
    }

    private void UnsubscribeFromProjectChanges()
    {
        if (_observedProjects != null) _observedProjects.CollectionChanged -= OnProjectsChanged;
        _observedProjects = null;
        if (_observedConnectors != null)
        {
            _observedConnectors.ConnectorsChanged -= OnConnectorsChanged;
            _observedConnectors.SignedIn -= OnConnectorSignedIn;
            _observedConnectors.ValuesChanged -= OnConnectorsChanged;
        }

        _observedConnectors = null;
        Interlocked.Exchange(ref _projectsRefreshCts, null)?.Cancel();
    }

    /// <summary>
    /// The runtime reads the connectors from its configuration; reloading applies edits to this chat. Connectors
    /// with values stored in OneWare are then restarted with those values.
    /// </summary>
    private void OnConnectorsChanged(object? sender, EventArgs e)
    {
        if (_session is not { } session) return;
        lock (_reportedMcpProblems) _reportedMcpProblems.Clear();
        _ = ApplyConnectorsAsync(session, reload: true);
    }

    private void OnConnectorSignedIn(object? sender, string serverName)
    {
        if (_session is not { } session) return;
        lock (_reportedMcpProblems) _reportedMcpProblems.Remove($"auth:{serverName}");
        _ = Task.Run(async () =>
        {
            try
            {
                // The tokens were stored by another runtime process; this one picks them up on reconnecting.
                await session.Rpc.Mcp.Oauth.AuthenticationStateChangedAsync(serverName);
                McpConnector? resolved;
                lock (_sessionConnectors) _sessionConnectors.TryGetValue(serverName, out resolved);
                await session.Rpc.Mcp.RestartServerAsync(serverName,
                    resolved == null ? null : JsonSerializer.SerializeToElement(resolved.ToConfig()));
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to reconnect MCP server {Name} after signing in.", serverName);
            }
        });
    }

    /// <summary>
    /// MCP servers for a new session: the connectors that use values stored in OneWare, with the values filled in.
    /// They replace the configuration's definitions, so the values reach only the server that references them
    /// instead of the runtime's environment, which every server inherits.
    /// </summary>
    private async Task<Dictionary<string, McpServerConfig>?> BuildConnectorServersAsync()
    {
        IReadOnlyList<McpConnector> resolved;
        try
        {
            resolved = await ContainerLocator.Container.Resolve<CopilotConnectorService>()
                .GetResolvedConnectorsAsync();
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Failed to read the connectors for the Copilot session.");
            resolved = [];
        }

        lock (_sessionConnectors)
        {
            _sessionConnectors.Clear();
            foreach (var connector in resolved) _sessionConnectors[connector.Name] = connector;
        }

        var servers = new Dictionary<string, McpServerConfig>(StringComparer.Ordinal);
        foreach (var connector in resolved)
            if (connector.ToServerConfig() is { } config)
                servers[connector.Name] = config;
        return servers.Count > 0 ? servers : null;
    }

    /// <summary>
    /// Starts the connectors with stored values on <paramref name="session" />. After <paramref name="reload" />ing
    /// the configuration all of them are restarted with their current values, and connectors that no longer use
    /// stored values go back to their configured definition.
    /// </summary>
    private async Task ApplyConnectorsAsync(CopilotSession session, bool reload)
    {
        var service = ContainerLocator.Container.Resolve<CopilotConnectorService>();
        try
        {
            await _connectorsGate.WaitAsync();
            _applyingConnectors = true;
            try
            {
                if (!ReferenceEquals(_session, session)) return;
                if (reload) await session.Rpc.Mcp.ReloadAsync();

                var resolved = await service.GetResolvedConnectorsAsync();
                List<string> previous;
                lock (_sessionConnectors)
                {
                    previous = _sessionConnectors.Keys.ToList();
                    _sessionConnectors.Clear();
                    foreach (var connector in resolved) _sessionConnectors[connector.Name] = connector;
                }

                // A new session already got the others through its configuration.
                foreach (var connector in resolved.Where(x => reload || x.ToServerConfig() == null))
                    await StartConnectorAsync(session, connector);

                if (!reload) return;
                var configured = await service.GetConfiguredConnectorsAsync();
                foreach (var name in previous.Where(x => resolved.All(r => r.Name != x)))
                {
                    // The session still runs it with the values it had before.
                    if (configured.FirstOrDefault(x => x.Name == name) is { Enabled: true } connector)
                        await StartConnectorAsync(session, connector);
                    else
                        await session.Rpc.Mcp.StopServerAsync(name);
                }
            }
            finally
            {
                _applyingConnectors = false;
                _connectorsGate.Release();
            }
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
        {
            // The session was closed meanwhile.
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Failed to apply the connectors to the Copilot session.");
        }
    }

    private static async Task StartConnectorAsync(CopilotSession session, McpConnector connector)
    {
        var config = JsonSerializer.SerializeToElement(connector.ToConfig());
        try
        {
            await session.Rpc.Mcp.RestartServerAsync(connector.Name, config);
        }
        catch (Exception e)
        {
            Logger.LogDebug(e, "Restarting MCP server {Name} failed, starting it.", connector.Name);
            await session.Rpc.Mcp.StartServerAsync(connector.Name, config);
        }
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
        lock (_reportedMcpProblems)
        {
            _reportedMcpProblems.Clear();
            _mcpSignInsInProgress.Clear();
        }
        _announcedWorkspaceKey = null;
        lock (_sessionConnectors) _sessionConnectors.Clear();
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

    // While connectors are reapplied, the configured definitions start briefly without the stored values.
    private bool IsApplyingConnector(string name)
    {
        if (!_applyingConnectors) return false;
        lock (_sessionConnectors) return _sessionConnectors.ContainsKey(name);
    }

    /// <summary>Shows an MCP problem once per chat.</summary>
    private bool TryReportMcpProblem(string key)
    {
        lock (_reportedMcpProblems) return _reportedMcpProblems.Add(key);
    }

    private void HandleMcpServerStatusChanged(SessionMcpServerStatusChangedData data)
    {
        if (_session is not { } session) return;

        if (data.Status == McpServerStatus.NeedsAuth)
        {
            OfferMcpSignIn(session, data.ServerName);
            return;
        }

        bool signingIn;
        lock (_reportedMcpProblems) signingIn = _mcpSignInsInProgress.Remove(data.ServerName);

        if (data.Status == McpServerStatus.Connected)
        {
            if (signingIn)
                EventReceived?.Invoke(this, new ChatMessageEvent($"Signed in to MCP server `{data.ServerName}`."));
        }
        else if (data.Status == McpServerStatus.Failed && !IsApplyingConnector(data.ServerName) &&
                 (signingIn || TryReportMcpProblem($"failed:{data.ServerName}")))
        {
            Logger.LogWarning("MCP server {Name} failed: {Error}", data.ServerName, data.Error);
            EventReceived?.Invoke(this, new ChatErrorEvent(
                $"MCP server '{data.ServerName}' failed: {data.Error ?? "unknown error"}. Check it in " +
                "Settings › AI Chat › Connectors."));
        }
    }

    /// <summary>
    /// Asks the user once per chat to sign in to a remote MCP server that requires OAuth. The runtime runs the flow
    /// and keeps the tokens in its keychain storage, so every chat can use them afterwards.
    /// </summary>
    private void OfferMcpSignIn(CopilotSession session, string serverName)
    {
        if (!TryReportMcpProblem($"auth:{serverName}")) return;

        var message = $"**MCP server `{serverName}` requires you to sign in.**\n\n" +
                      "The sign-in page opens in your browser. The access token is kept by the Copilot CLI " +
                      "in your system keychain.";
        EventReceived?.Invoke(this, new ChatPermissionRequestEvent(message, "Sign in", "Not now",
            new RelayCommand<Control?>(_ => SignInToMcpServerAsync(session, serverName)),
            new RelayCommand<Control?>(_ => { })));
    }

    private async Task SignInToMcpServerAsync(CopilotSession session, string serverName)
    {
        var connectors = ContainerLocator.Container.Resolve<CopilotConnectorService>();
        try
        {
            McpConnector? connector = null;
            try
            {
                connector = (await connectors.ListAsync()).FirstOrDefault(x => x.Name == serverName);
            }
            catch (Exception e)
            {
                Logger.LogDebug(e, "Failed to look up connector {Name}.", serverName);
            }

            if (connector != null)
            {
                // Signs in on a separate runtime with the connector's OAuth app; open chats reconnect afterwards.
                var status = await connectors.SignInAsync(connector);
                if (status.State == McpConnectorState.Connected)
                    EventReceived?.Invoke(this, new ChatMessageEvent($"Signed in to MCP server `{serverName}`."));
                else
                    EventReceived?.Invoke(this, new ChatErrorEvent(
                        $"Signing in to MCP server '{serverName}' failed: {status.Message ?? "not completed"}"));
                return;
            }

            // A server of the project's own configuration.
            lock (_reportedMcpProblems) _mcpSignInsInProgress.Add(serverName);
            var result = await session.Rpc.Mcp.Oauth.LoginAsync(new McpOauthLoginRequest
            {
                ServerName = serverName,
                ClientName = "OneWare Studio",
                CallbackSuccessMessage = "Signed in. You can close this tab and return to OneWare Studio."
            });

            // Without a URL cached tokens were still valid and the server is already reconnected.
            if (!string.IsNullOrEmpty(result.AuthorizationUrl))
                PlatformHelper.OpenHyperLink(result.AuthorizationUrl);
        }
        catch (Exception e)
        {
            lock (_reportedMcpProblems) _mcpSignInsInProgress.Remove(serverName);
            Logger.LogWarning(e, "Failed to sign in to MCP server {Name}.", serverName);
            EventReceived?.Invoke(this, new ChatErrorEvent(
                $"Signing in to MCP server '{serverName}' failed: " +
                CopilotConnectorService.DescribeSignInError(e.Message)));
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
