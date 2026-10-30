using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Extensions.Logging;
using OneWare.Copilot.Connectors;
using OneWare.Copilot.ViewModels;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Copilot.Services;

/// <summary>
/// MCP servers of a chat: the connectors (IDE-wide servers from OneWare's <c>mcp.json</c>) are given to the session
/// as its own servers and kept in sync by diffing, servers of the project's configuration are started by the runtime
/// once the user trusts the folder, and the Copilot CLI's user servers are left out (they can be imported instead).
/// Problems are shown as a badge in the chat footer instead of chat messages.
/// </summary>
public abstract partial class CopilotChatServiceBase
{
    // Project files that define MCP servers the runtime loads for trusted folders.
    private static readonly string[] ProjectMcpFiles = [".mcp.json", Path.Combine(".vscode", "mcp.json")];

    // Servers turned off per chat, by session id, for the app run.
    private static readonly ConcurrentDictionary<string, HashSet<string>> ChatDisabledMcpServers = new();

    // Folders whose MCP servers the user chose not to start, for the app run.
    private static readonly ConcurrentDictionary<string, byte> DeclinedTrustFolders =
        new(CopilotSessionConfiguration.PathComparer);

    // Connectors running in the session, with the fingerprint of the configuration they were started with.
    private readonly Dictionary<string, string> _appliedConnectors = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _mcpGate = new(1, 1);
    private McpConnectorHub? _observedHub;
    private CancellationTokenSource? _mcpRefreshCts;
    private int _mcpProblemCount;
    private IRelayCommand<Control?>? _manageMcpServersCommand;
    private IAsyncRelayCommand? _refreshMcpServersCommand;

    /// <summary>MCP servers of this chat, for the footer.</summary>
    public ObservableCollection<CopilotMcpServerViewModel> McpServers { get; } = [];

    public bool HasMcpServers => McpServers.Count > 0;

    public int McpProblemCount
    {
        get => _mcpProblemCount;
        private set
        {
            if (!SetProperty(ref _mcpProblemCount, value)) return;
            OnPropertyChanged(nameof(HasMcpProblems));
            OnPropertyChanged(nameof(McpSummary));
        }
    }

    public bool HasMcpProblems => McpProblemCount > 0;

    public string McpSummary
    {
        get
        {
            var connected = McpServers.Count(x => x.State == McpConnectorState.Connected);
            var text = $"MCP servers: {connected} of {McpServers.Count} connected";
            return HasMcpProblems ? $"{text}, {McpProblemCount} need attention" : text;
        }
    }

    public IRelayCommand<Control?> ManageMcpServersCommand =>
        _manageMcpServersCommand ??= new RelayCommand<Control?>(owner =>
            CopilotConnectorsSettingViewModel.ShowWindow(Hub, owner));

    /// <summary>Refreshes the list including tool counts, e.g. when the footer flyout opens.</summary>
    public IAsyncRelayCommand RefreshMcpServersCommand =>
        _refreshMcpServersCommand ??= new AsyncRelayCommand(() => RefreshMcpServersAsync(includeTools: true));

    private static McpConnectorHub Hub => ContainerLocator.Container.Resolve<McpConnectorHub>();

    private HashSet<string> GetChatDisabledMcpServers() =>
        CurrentSessionId is { } id ? ChatDisabledMcpServers.GetOrAdd(id, _ => new HashSet<string>(StringComparer.Ordinal)) : [];

    private static HashSet<string> GetChatDisabledMcpServers(string? sessionId) =>
        sessionId is { } id && ChatDisabledMcpServers.TryGetValue(id, out var names) ? names : [];

    private void SubscribeToConnectors()
    {
        if (_observedHub != null) return;
        _observedHub = Hub;
        _observedHub.Changed += OnConnectorsChanged;
        _observedHub.SignedIn += OnConnectorSignedIn;
    }

    private void UnsubscribeFromConnectors()
    {
        if (_observedHub == null) return;
        _observedHub.Changed -= OnConnectorsChanged;
        _observedHub.SignedIn -= OnConnectorSignedIn;
        _observedHub = null;
    }

    private void ResetMcpState()
    {
        Interlocked.Exchange(ref _mcpRefreshCts, null)?.Cancel();
        lock (_appliedConnectors) _appliedConnectors.Clear();
        Dispatcher.UIThread.Post(() =>
        {
            McpServers.Clear();
            UpdateMcpSummary();
        });
    }

    /// <summary>
    /// Configures the MCP servers of a new or resumed session: connectors that the session configuration can express
    /// are passed directly, the Copilot CLI's user servers and servers turned off in this chat are disabled.
    /// </summary>
    private async Task ConfigureMcpAsync(SessionConfigBase config, string? sessionId)
    {
        IReadOnlyList<ResolvedMcpConnector> connectors = [];
        IReadOnlyList<string> cliServers = [];
        try
        {
            connectors = await Hub.ResolveEnabledAsync();
            cliServers = await Hub.GetCopilotCliServerNamesAsync();
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Failed to read the MCP servers for the Copilot session.");
        }

        var chatDisabled = GetChatDisabledMcpServers(sessionId);
        var connectorNames = connectors.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);

        var disabled = cliServers.Where(x => !connectorNames.Contains(x))
            .Concat(chatDisabled.Where(x => !connectorNames.Contains(x)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (disabled.Count > 0) config.DisabledMcpServers = disabled;

        var servers = new Dictionary<string, McpServerConfig>(StringComparer.Ordinal);
        lock (_appliedConnectors)
        {
            _appliedConnectors.Clear();
            foreach (var connector in connectors.Where(x => !chatDisabled.Contains(x.Name)))
            {
                if (connector.ServerConfig is not { } serverConfig) continue;
                servers[connector.Name] = serverConfig;
                _appliedConnectors[connector.Name] = connector.Fingerprint;
            }
        }

        if (servers.Count > 0) config.McpServers = servers;
    }

    /// <summary>Runs once the session exists: starts what the configuration couldn't and checks project servers.</summary>
    private void OnMcpSessionStarted(CopilotSession session, string workingDirectory)
    {
        _ = Task.Run(async () =>
        {
            await ApplyConnectorsAsync(session);
            await RefreshMcpServersAsync(includeTools: false, disableCliServers: true);
            await OfferProjectTrustAsync(session, workingDirectory);
        });
    }

    private void OnConnectorsChanged(object? sender, EventArgs e)
    {
        if (_session is not { } session) return;
        _ = Task.Run(async () =>
        {
            await ApplyConnectorsAsync(session);
            await RefreshMcpServersAsync(includeTools: false);
        });
    }

    private void OnConnectorSignedIn(object? sender, string serverName)
    {
        if (_session is not { } session) return;
        _ = Task.Run(async () =>
        {
            try
            {
                // The tokens may have been stored by another runtime process; this one picks them up on reconnecting.
                await session.Rpc.Mcp.Oauth.AuthenticationStateChangedAsync(serverName);
                bool isConnector;
                lock (_appliedConnectors) isConnector = _appliedConnectors.ContainsKey(serverName);
                if (isConnector) await ApplyConnectorsAsync(session, restart: serverName);
                else await session.Rpc.Mcp.RestartServerAsync(serverName, null);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to reconnect MCP server {Name} after signing in.", serverName);
            }

            await RefreshMcpServersAsync(includeTools: false);
        });
    }

    /// <summary>
    /// Brings the session's connectors in line with the enabled ones: removed connectors are stopped, new ones
    /// started and changed ones restarted. <paramref name="restart" /> restarts one (or <c>"*"</c> all) regardless.
    /// </summary>
    private async Task ApplyConnectorsAsync(CopilotSession session, string? restart = null)
    {
        try
        {
            await _mcpGate.WaitAsync();
            try
            {
                if (!ReferenceEquals(_session, session)) return;

                var chatDisabled = GetChatDisabledMcpServers();
                var desired = (await Hub.ResolveEnabledAsync())
                    .Where(x => !chatDisabled.Contains(x.Name))
                    .ToDictionary(x => x.Name, StringComparer.Ordinal);

                Dictionary<string, string> applied;
                lock (_appliedConnectors) applied = new Dictionary<string, string>(_appliedConnectors);

                foreach (var name in applied.Keys.Where(x => !desired.ContainsKey(x)))
                {
                    await TryMcpAsync(() => session.Rpc.Mcp.StopServerAsync(name), "stop", name);
                    applied.Remove(name);
                }

                foreach (var (name, connector) in desired)
                {
                    var running = applied.TryGetValue(name, out var fingerprint);
                    if (running && fingerprint == connector.Fingerprint && restart != "*" && restart != name) continue;

                    if (running) await RestartConnectorAsync(session, connector);
                    else await StartConnectorAsync(session, connector);
                    applied[name] = connector.Fingerprint;
                }

                lock (_appliedConnectors)
                {
                    _appliedConnectors.Clear();
                    foreach (var (name, fingerprint) in applied) _appliedConnectors[name] = fingerprint;
                }
            }
            finally
            {
                _mcpGate.Release();
            }
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
        {
            // The session was closed meanwhile.
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Failed to apply the MCP servers to the Copilot session.");
        }
    }

    private static async Task StartConnectorAsync(CopilotSession session, ResolvedMcpConnector connector)
    {
        try
        {
            await session.Rpc.Mcp.StartServerAsync(connector.Name, connector.Config);
        }
        catch (Exception e) when (e is not (OperationCanceledException or ObjectDisposedException))
        {
            // E.g. the session configuration already started it.
            Logger.LogDebug(e, "Starting MCP server {Name} failed, restarting it.", connector.Name);
            await RestartConnectorAsync(session, connector);
        }
    }

    private static Task RestartConnectorAsync(CopilotSession session, ResolvedMcpConnector connector) =>
        TryMcpAsync(() => session.Rpc.Mcp.RestartServerAsync(connector.Name, connector.Config), "restart",
            connector.Name);

    private static async Task TryMcpAsync(Func<Task> action, string operation, string name)
    {
        try
        {
            await action();
        }
        catch (Exception e) when (e is not (OperationCanceledException or ObjectDisposedException))
        {
            // The status shows the problem.
            Logger.LogDebug(e, "Failed to {Operation} MCP server {Name}.", operation, name);
        }
    }

    private void HandleMcpServerStatusChanged(SessionMcpServerStatusChangedData data)
    {
        var status = McpConnectorHub.ToStatus(data.Status, data.Error);
        if (data.Status == McpServerStatus.Failed)
            Logger.LogWarning("MCP server {Name} failed: {Error}", data.ServerName, data.Error);

        bool isConnector;
        lock (_appliedConnectors) isConnector = _appliedConnectors.ContainsKey(data.ServerName);
        if (isConnector) Hub.ReportStatus(data.ServerName, status);

        Dispatcher.UIThread.Post(() =>
        {
            if (McpServers.FirstOrDefault(x => x.Name == data.ServerName) is { } item)
            {
                item.Update(data.Status, data.Error);
                UpdateMcpSummary();
            }
            else
            {
                ScheduleMcpRefresh();
            }
        });
    }

    private void ScheduleMcpRefresh()
    {
        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _mcpRefreshCts, cts)?.Cancel();
        _ = Task.Delay(TimeSpan.FromMilliseconds(300), cts.Token).ContinueWith(
            _ => RefreshMcpServersAsync(includeTools: false), TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    /// <summary>Rebuilds <see cref="McpServers" /> from the session.</summary>
    private async Task RefreshMcpServersAsync(bool includeTools, bool disableCliServers = false)
    {
        if (_session is not { } session) return;
        try
        {
            var servers = (await session.Rpc.Mcp.ListAsync()).Servers;
            HashSet<string> connectors;
            lock (_appliedConnectors) connectors = _appliedConnectors.Keys.ToHashSet(StringComparer.Ordinal);
            var configured = (await Hub.GetConnectorsAsync()).Where(x => x.Enabled)
                .Select(x => x.Name).ToHashSet(StringComparer.Ordinal);

            // In case the runtime started user servers despite the session configuration.
            if (disableCliServers)
                foreach (var server in servers.Where(x => x.Source == McpServerSource.User &&
                                                          !configured.Contains(x.Name) &&
                                                          x.Status != McpServerStatus.Disabled))
                    await TryMcpAsync(() => session.Rpc.Mcp.DisableAsync(server.Name), "disable", server.Name);

            var chatDisabled = GetChatDisabledMcpServers();
            var items = new List<CopilotMcpServerViewModel>();
            foreach (var server in servers)
            {
                var isConnector = connectors.Contains(server.Name) || configured.Contains(server.Name);
                // User servers OneWare doesn't own are not part of the chat.
                if (!isConnector && server.Source == McpServerSource.User) continue;

                var item = new CopilotMcpServerViewModel(this, server.Name, server.DisplayName,
                    isConnector ? "Connector" : DescribeSource(server.Source), isConnector,
                    !chatDisabled.Contains(server.Name));
                item.Update(server.Status, server.Error);
                if (includeTools && server.Status == McpServerStatus.Connected)
                {
                    try
                    {
                        item.ToolCount = (await session.Rpc.Mcp.ListToolsAsync(server.Name)).Tools.Count;
                    }
                    catch (Exception e)
                    {
                        Logger.LogDebug(e, "Failed to list the tools of MCP server {Name}.", server.Name);
                    }
                }

                items.Add(item);
            }

            // Connectors turned off in this chat aren't running, so the session doesn't list them.
            foreach (var name in configured.Where(x => chatDisabled.Contains(x) && items.All(i => i.Name != x)))
                items.Add(new CopilotMcpServerViewModel(this, name, null, "Connector", true, false));

            if (!ReferenceEquals(_session, session)) return;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                McpServers.Clear();
                foreach (var item in items.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)) McpServers.Add(item);
                UpdateMcpSummary();
            });
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
        {
            // The session was closed meanwhile.
        }
        catch (Exception e)
        {
            Logger.LogDebug(e, "Failed to list the MCP servers of the Copilot session.");
        }
    }

    private static string DescribeSource(McpServerSource? source) =>
        source == McpServerSource.Workspace ? "Project"
        : source == McpServerSource.Plugin ? "Plugin"
        : source == McpServerSource.Builtin ? "Built-in"
        : source == McpServerSource.Managed ? "Managed"
        : source == McpServerSource.Account ? "Account"
        : "Session";

    private void UpdateMcpSummary()
    {
        McpProblemCount = McpServers.Count(x => x.HasProblem);
        OnPropertyChanged(nameof(HasMcpServers));
        OnPropertyChanged(nameof(McpSummary));
    }

    /// <summary>Turns a server on or off in this chat only.</summary>
    internal async Task SetMcpServerEnabledInChatAsync(CopilotMcpServerViewModel item, bool enabled)
    {
        if (_session is not { } session) return;

        var chatDisabled = GetChatDisabledMcpServers();
        lock (chatDisabled)
        {
            if (enabled) chatDisabled.Remove(item.Name);
            else chatDisabled.Add(item.Name);
        }

        if (item.IsConnector)
            await ApplyConnectorsAsync(session);
        else if (enabled)
            await TryMcpAsync(() => session.Rpc.Mcp.EnableAsync(item.Name), "enable", item.Name);
        else
            await TryMcpAsync(() => session.Rpc.Mcp.DisableAsync(item.Name), "disable", item.Name);

        await RefreshMcpServersAsync(includeTools: true);
    }

    internal async Task SignInToMcpServerAsync(CopilotMcpServerViewModel item)
    {
        if (_session is not { } session) return;

        // Connectors sign in on a separate runtime with their OAuth app; every chat reconnects afterwards.
        var status = item.IsConnector
            ? await Hub.SignInAsync(item.Name)
            : await Hub.SignInAsync(session, item.Name);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            item.Message = status.State == McpConnectorState.Connected ? null : status.Message;
            if (status.State == McpConnectorState.Failed) item.SetFailed(status.Message);
            UpdateMcpSummary();
        });
    }

    /// <summary>
    /// The runtime only starts the MCP servers of a project's configuration once the folder is trusted. Asks once
    /// per folder and app run whether to trust it.
    /// </summary>
    private async Task OfferProjectTrustAsync(CopilotSession session, string folder)
    {
        try
        {
            var file = ProjectMcpFiles.FirstOrDefault(x => File.Exists(Path.Combine(folder, x)));
            if (file == null || DeclinedTrustFolders.ContainsKey(folder)) return;
            if ((await session.Rpc.Permissions.FolderTrust.IsTrustedAsync(folder)).Trusted) return;
            if (!ReferenceEquals(_session, session)) return;

            var message = $"**This project defines MCP servers** (`{file.Replace('\\', '/')}`).\n\n" +
                          "They run commands or connect to services on your behalf. Start them only if you trust " +
                          $"`{folder}`.";
            EventReceived?.Invoke(this, new ChatPermissionRequestEvent(message, "Trust and Start", "Not Now",
                new AsyncRelayCommand<Control?>(_ => TrustProjectAsync(session, folder)),
                new RelayCommand<Control?>(_ => DeclinedTrustFolders.TryAdd(folder, 0))));
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
        {
            // The session was closed meanwhile.
        }
        catch (Exception e)
        {
            Logger.LogDebug(e, "Failed to check whether {Folder} is trusted.", folder);
        }
    }

    private async Task TrustProjectAsync(CopilotSession session, string folder)
    {
        try
        {
            await session.Rpc.Permissions.FolderTrust.AddTrustedAsync(folder);
            await session.Rpc.Mcp.ReloadAsync();

            // Reloading reads the configuration again, so servers this chat leaves out are disabled again and the
            // connectors restarted in case the reload replaced them.
            var configured = (await Hub.GetConnectorsAsync()).Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
            var leaveOut = (await Hub.GetCopilotCliServerNamesAsync()).Where(x => !configured.Contains(x))
                .Concat(GetChatDisabledMcpServers().Where(x => !configured.Contains(x)));
            foreach (var name in leaveOut.Distinct(StringComparer.Ordinal))
                await TryMcpAsync(() => session.Rpc.Mcp.DisableAsync(name), "disable", name);

            await ApplyConnectorsAsync(session, restart: "*");
            await RefreshMcpServersAsync(includeTools: false);
        }
        catch (Exception e) when (e is not (OperationCanceledException or ObjectDisposedException))
        {
            Logger.LogWarning(e, "Failed to start the MCP servers of {Folder}.", folder);
            EventReceived?.Invoke(this, new ChatErrorEvent(
                $"Failed to start the project's MCP servers: {McpConnectorHub.StripRpcPrefix(e.Message)}"));
        }
    }
}

/// <summary>An MCP server of a chat, shown in the footer flyout.</summary>
public sealed class CopilotMcpServerViewModel : ObservableObject
{
    private readonly CopilotChatServiceBase _chat;
    private bool _enabledInChat;
    private McpConnectorState _state;
    private string? _message;
    private int? _toolCount;
    private bool _isBusy;

    internal CopilotMcpServerViewModel(CopilotChatServiceBase chat, string name, string? displayName, string source,
        bool isConnector, bool enabledInChat)
    {
        _chat = chat;
        Name = name;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        Source = source;
        IsConnector = isConnector;
        _enabledInChat = enabledInChat;
        SignInCommand = new AsyncRelayCommand(SignInAsync);
    }

    public string Name { get; }

    public string DisplayName { get; }

    public string Source { get; }

    public bool IsConnector { get; }

    public bool EnabledInChat
    {
        get => _enabledInChat;
        set
        {
            if (!SetProperty(ref _enabledInChat, value)) return;
            _ = RunAsync(() => _chat.SetMcpServerEnabledInChatAsync(this, value));
        }
    }

    public McpConnectorState State
    {
        get => _state;
        private set
        {
            if (!SetProperty(ref _state, value)) return;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(NeedsSignIn));
            OnPropertyChanged(nameof(HasProblem));
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(IsFailed));
        }
    }

    public string? Message
    {
        get => _message;
        set
        {
            if (SetProperty(ref _message, value)) OnPropertyChanged(nameof(HasMessage));
        }
    }

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public int? ToolCount
    {
        get => _toolCount;
        set
        {
            if (SetProperty(ref _toolCount, value)) OnPropertyChanged(nameof(StatusText));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsConnected => State == McpConnectorState.Connected;

    public bool IsFailed => State == McpConnectorState.Failed;

    public bool NeedsSignIn => State == McpConnectorState.NeedsSignIn && EnabledInChat;

    public bool HasProblem => EnabledInChat && State is McpConnectorState.Failed or McpConnectorState.NeedsSignIn;

    public string StatusText => !EnabledInChat
        ? $"{Source} · Off in this chat"
        : State switch
        {
            McpConnectorState.Connected => ToolCount is { } count
                ? $"{Source} · {count} tool{(count == 1 ? "" : "s")}"
                : $"{Source} · Connected",
            McpConnectorState.Pending => $"{Source} · Starting…",
            McpConnectorState.NeedsSignIn => $"{Source} · Sign-in required",
            McpConnectorState.Failed => $"{Source} · Failed",
            _ => $"{Source} · Stopped"
        };

    public IAsyncRelayCommand SignInCommand { get; }

    internal void Update(McpServerStatus status, string? error)
    {
        var converted = McpConnectorHub.ToStatus(status, error);
        // Disabled servers have the toggle off; they are not a problem.
        if (status == McpServerStatus.Disabled) _enabledInChat = false;
        State = converted.State;
        Message = converted.State == McpConnectorState.Failed ? converted.Message : null;
        OnPropertyChanged(nameof(EnabledInChat));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(NeedsSignIn));
        OnPropertyChanged(nameof(HasProblem));
    }

    internal void SetFailed(string? message)
    {
        State = McpConnectorState.Failed;
        Message = McpConnectorHub.DescribeSignInError(message ?? "Unknown error");
    }

    private Task SignInAsync() => RunAsync(() => _chat.SignInToMcpServerAsync(this));

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action();
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(HasProblem));
            OnPropertyChanged(nameof(NeedsSignIn));
        }
    }
}
