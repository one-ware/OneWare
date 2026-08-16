using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dotacp.client.unstable;
using dotacp.protocol.unstable;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Acp.Services;

/// <summary>
/// Abstract base for any ACP-over-stdio chat service.
/// Subclasses supply the binary path, startup arguments, and install UX.
/// </summary>
public abstract class AcpChatService(IPaths paths, ILogger logger)
    : ObservableObject, IChatService, IAcpClient
{
    // ── Abstract surface ──────────────────────────────────────────────────────

    public abstract string Name { get; }

    /// <summary>Return the full path to the agent binary, or <see langword="null"/> when not installed.</summary>
    protected abstract string? ResolveAgentPath();

    /// <summary>Command-line arguments for the agent. Override to add e.g. <c>["--acp"]</c>.</summary>
    protected virtual IEnumerable<string> GetAgentArguments() => [];

    /// <summary>
    /// Environment variables to inject into the agent process.
    /// Override to supply e.g. <c>OPENAI_API_KEY</c> from a setting.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, string> GetEnvironmentVariables() =>
        new Dictionary<string, string>();

    /// <summary>Called when the binary could not be found. Fire an install button via EventReceived.</summary>
    protected abstract void OnAgentNotFound();

    /// <summary>
    /// Called when the agent requires env-var credentials that are not yet configured.
    /// Default implementation emits a <see cref="ChatErrorEvent"/> listing the missing variables.
    /// </summary>
    protected virtual void OnAuthRequired(AuthMethodEnvVar method, IReadOnlyList<AuthEnvVar> missingVars)
    {
        var varNames = string.Join(", ", missingVars.Select(v => v.Label ?? v.Name));
        var link = method.Link;
        var msg = link != null
            ? $"Authentication required. Please configure: {varNames}\n\nGet your API key: {link}"
            : $"Authentication required. Please configure: {varNames}";
        EventReceived?.Invoke(this, new ChatErrorEvent(msg));
    }

    /// <summary>
    /// Called when the agent requires interactive terminal authentication (e.g. ChatGPT OAuth).
    /// Override to show a login button or otherwise guide the user.
    /// </summary>
    protected virtual void OnTerminalAuthRequired(AuthMethodTerminal method)
    {
        var name = method.Name ?? "interactive login";
        EventReceived?.Invoke(this, new ChatErrorEvent(
            $"Authentication requires {name}. " +
            "Please authenticate via a terminal before connecting."));
    }

    /// <summary>
    /// Called when the agent advertises an agent-managed auth method (e.g. browser-based
    /// ChatGPT OAuth via <c>AuthMethodAgent</c>).  The connection is kept alive so that
    /// <see cref="PerformAgentAuthAndOpenSessionAsync"/> can be called from the button handler.
    /// Override to surface a login button.  Default implementation emits a
    /// <see cref="ChatErrorEvent"/> with the method description.
    /// </summary>
    protected virtual void OnAgentAuthRequired(AuthMethodAgent method)
    {
        var name = method.Description ?? method.Name ?? "agent-managed login";
        EventReceived?.Invoke(this, new ChatErrorEvent(
            $"Authentication required: {name}. " +
            "Please complete the login flow before connecting."));
    }

    /// <summary>
    /// Authenticates using the given <paramref name="methodId"/> on the current live connection
    /// and then opens a new session.  Call this from an <see cref="OnAgentAuthRequired"/>
    /// override button handler.  If the connection has been lost in the meantime, falls back
    /// to a full <see cref="InitializeAsync"/> reconnect.
    /// </summary>
    protected async Task PerformAgentAuthAndOpenSessionAsync(string methodId)
    {
        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_connection == null)
            {
                // Connection was lost while the user was reading the prompt; reconnect from scratch.
                await ConnectAsync().ConfigureAwait(false);
                return;
            }

            await _connection.AuthenticateAsync(
                new AuthenticateRequest { MethodId = methodId }).ConfigureAwait(false);

            var sessionResponse = await _connection.NewSessionAsync(
                new NewSessionRequest { Cwd = paths.ProjectsDirectory, McpServers = [] })
                .ConfigureAwait(false);

            _sessionId = sessionResponse.SessionId.ToString();
            StatusChanged?.Invoke(this, new StatusEvent(true, $"{Name} ready"));
            SessionReset?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Agent auth failed for method {MethodId}", methodId);
            await DisposeConnectionAsync().ConfigureAwait(false);
            EventReceived?.Invoke(this, new ChatErrorEvent($"Authentication failed: {ex.Message}"));
        }
        finally
        {
            _initLock.Release();
        }
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private Process? _agentProcess;
    private Connection? _connection;
    private string? _sessionId;
    private readonly StringBuilder _stderrBuffer = new();

    private CancellationTokenSource? _promptCts;
    private string? _currentMessageId;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TerminalEntry> _terminals = new();

    // ── IChatService ──────────────────────────────────────────────────────────

    public Control? BottomUiExtension => null;

    public event EventHandler? SessionReset;
    public event EventHandler<ChatEvent>? EventReceived;
    public event EventHandler<StatusEvent>? StatusChanged;

    // Protected helpers so subclasses can raise events without reflection tricks
    protected void RaiseSessionReset() => SessionReset?.Invoke(this, EventArgs.Empty);
    protected void RaiseEventReceived(ChatEvent e) => EventReceived?.Invoke(this, e);
    protected void RaiseStatusChanged(StatusEvent e) => StatusChanged?.Invoke(this, e);

    public async Task<bool> InitializeAsync()
    {
        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await DisposeConnectionAsync().ConfigureAwait(false);
            return await ConnectAsync().ConfigureAwait(false);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task SendAsync(string prompt)
    {
        if (_connection == null || _sessionId == null)
        {
            EventReceived?.Invoke(this, new ChatErrorEvent("Agent is not connected."));
            return;
        }

        _currentMessageId = Guid.NewGuid().ToString("N");
        _promptCts?.Dispose();
        _promptCts = new CancellationTokenSource();

        try
        {
            await _connection.PromptAsync(
                new PromptRequest
                {
                    SessionId = new SessionId(_sessionId),
                    Prompt = [new TextContent { Text = prompt }]
                },
                _promptCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "ACP PromptAsync error");
            EventReceived?.Invoke(this, new ChatErrorEvent(ex.Message));
        }
        finally
        {
            _currentMessageId = null;
            EventReceived?.Invoke(this, new ChatIdleEvent());
        }
    }

    public async Task AbortAsync()
    {
        if (_promptCts is { } cts)
        {
            try
            {
                await cts.CancelAsync().ConfigureAwait(false);
                if (_connection != null && _sessionId != null)
                    await _connection.CancelAsync(
                        new CancelNotification { SessionId = new SessionId(_sessionId) })
                        .ConfigureAwait(false);
            }
            catch (Exception ex) { logger.LogWarning(ex, "ACP abort failed"); }
        }
    }

    public async Task NewChatAsync()
    {
        if (_connection == null) return;

        try
        {
            if (_sessionId != null)
                await _connection.CloseAsync(
                    new CloseSessionRequest { SessionId = new SessionId(_sessionId) })
                    .ConfigureAwait(false);
        }
        catch (Exception ex) { logger.LogWarning(ex, "ACP close session failed"); }

        _sessionId = null;

        try
        {
            var resp = await _connection.NewSessionAsync(
                new NewSessionRequest { Cwd = paths.ProjectsDirectory, McpServers = [] })
                .ConfigureAwait(false);
            _sessionId = resp.SessionId.ToString();
            SessionReset?.Invoke(this, EventArgs.Empty);
            StatusChanged?.Invoke(this, new StatusEvent(true, $"{Name} ready"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ACP new session failed");
            EventReceived?.Invoke(this, new ChatErrorEvent($"Failed to create session: {ex.Message}"));
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeConnectionAsync().ConfigureAwait(false);
    }

    // ── Connection lifecycle ──────────────────────────────────────────────────

    private async Task<bool> ConnectAsync()
    {
        var agentPath = ResolveAgentPath();

        if (agentPath == null)
        {
            OnAgentNotFound();
            return false;
        }

        try
        {
            StatusChanged?.Invoke(this, new StatusEvent(false, $"Starting {Name}…"));

            _stderrBuffer.Clear();
            var psi = new ProcessStartInfo(agentPath)
            {
                WorkingDirectory = paths.ProjectsDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Use Arguments (raw string) instead of ArgumentList to avoid the
            // "-- <arg>" quoting artifact that occurs when the binary is a .cmd wrapper on Windows.
            var extraArgs = GetAgentArguments().ToList();
            if (extraArgs.Count > 0)
                psi.Arguments = string.Join(" ", extraArgs);

            // Inject credentials and other env vars supplied by the subclass
            var envVars = GetEnvironmentVariables();
            foreach (var (k, v) in envVars)
                psi.EnvironmentVariables[k] = v;

            _agentProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _agentProcess.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lock (_stderrBuffer) _stderrBuffer.AppendLine(e.Data);
                logger.LogDebug("[{Name} stderr] {Line}", Name, e.Data);
            };
            _agentProcess.Exited += OnAgentProcessExited;

            if (!_agentProcess.Start())
            {
                StatusChanged?.Invoke(this, new StatusEvent(false, "Failed to start agent"));
                return false;
            }

            _agentProcess.BeginErrorReadLine();

            _connection = Connection.RunClient(
                this,
                _agentProcess.StandardInput.BaseStream,
                _agentProcess.StandardOutput.BaseStream);

            if (_connection == null)
            {
                StatusChanged?.Invoke(this, new StatusEvent(false, "Connection failed"));
                return false;
            }

            var initResponse = await _connection.InitializeAsync(new InitializeRequest
            {
                ProtocolVersion = ProtocolMeta.Version,
                ClientCapabilities = new ClientCapabilities
                {
                    Fs = new FileSystemCapabilities { ReadTextFile = true, WriteTextFile = true },
                    Terminal = true
                },
                ClientInfo = new Implementation { Name = "OneWare Studio", Version = "1.0" }
            }).ConfigureAwait(false);

            if (initResponse.AuthMethods is { Length: > 0 } methods)
            {
                // Prefer env-var auth (e.g. OPENAI_API_KEY for codex-acp)
                var envVarMethod  = methods.OfType<AuthMethodEnvVar>().FirstOrDefault();
                var termMethod    = methods.OfType<AuthMethodTerminal>().FirstOrDefault();
                var agentMethod   = methods.OfType<AuthMethodAgent>().FirstOrDefault();

                if (envVarMethod != null)
                {
                    var missingVars = (envVarMethod.Vars ?? [])
                        .Where(v => !v.Optional &&
                                    string.IsNullOrEmpty(Environment.GetEnvironmentVariable(v.Name)) &&
                                    !envVars.ContainsKey(v.Name))
                        .ToList();

                    if (missingVars.Count > 0)
                    {
                        // Prefer interactive login when env-var credentials are missing.
                        // Agent auth (e.g. ChatGPT OAuth) keeps the connection alive so that
                        // PerformAgentAuthAndOpenSessionAsync can call AuthenticateAsync on it.
                        if (termMethod != null)
                        {
                            await DisposeConnectionAsync().ConfigureAwait(false);
                            OnTerminalAuthRequired(termMethod);
                        }
                        else if (agentMethod != null)
                        {
                            // Keep connection alive — PerformAgentAuthAndOpenSessionAsync needs it.
                            OnAgentAuthRequired(agentMethod);
                        }
                        else
                        {
                            await DisposeConnectionAsync().ConfigureAwait(false);
                            OnAuthRequired(envVarMethod, missingVars);
                        }
                        return false;
                    }

                    await _connection.AuthenticateAsync(
                        new AuthenticateRequest { MethodId = envVarMethod.Id })
                        .ConfigureAwait(false);
                }
                else if (termMethod != null)
                {
                    // No env-var method — only terminal auth available
                    await DisposeConnectionAsync().ConfigureAwait(false);
                    OnTerminalAuthRequired(termMethod);
                    return false;
                }
                else if (agentMethod != null)
                {
                    // Agent handles auth itself (e.g. browser-based ChatGPT OAuth).
                    // Keep the connection alive and surface a button so the user can
                    // consciously start the login flow.
                    OnAgentAuthRequired(agentMethod);
                    return false;
                }
            }

            var sessionResponse = await _connection.NewSessionAsync(
                new NewSessionRequest { Cwd = paths.ProjectsDirectory, McpServers = [] })
                .ConfigureAwait(false);

            _sessionId = sessionResponse.SessionId.ToString();
            StatusChanged?.Invoke(this, new StatusEvent(true, $"{Name} ready"));
            return true;
        }
        catch (Exception ex)
        {
            var stderr = GetStderr();
            logger.LogError(ex, "ACP connect failed. stderr={Stderr}", stderr);
            StatusChanged?.Invoke(this, new StatusEvent(false, "Agent unavailable"));
            var msg = stderr is { Length: > 0 }
                ? $"{ex.Message}\n\nAgent output:\n{stderr}"
                : ex.Message;
            EventReceived?.Invoke(this, new ChatErrorEvent(msg));
            return false;
        }
    }

    private async Task DisposeConnectionAsync()
    {
        if (_promptCts != null)
        {
            await _promptCts.CancelAsync().ConfigureAwait(false);
            _promptCts.Dispose();
            _promptCts = null;
        }

        foreach (var (_, entry) in _terminals) KillTerminalEntry(entry);
        _terminals.Clear();

        var conn = _connection;
        _connection = null;
        _sessionId = null;

        if (conn != null)
        {
            try { conn.Dispose(); }
            catch (Exception ex) { logger.LogWarning(ex, "ACP connection dispose error"); }
        }

        var proc = _agentProcess;
        _agentProcess = null;

        if (proc != null)
        {
            proc.Exited -= OnAgentProcessExited;
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                    await proc.WaitForExitAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex) { logger.LogWarning(ex, "ACP agent process kill error"); }
            proc.Dispose();
        }
    }

    private void OnAgentProcessExited(object? sender, EventArgs e)
    {
        if (sender is not Process proc) return;
        var exitCode = proc.ExitCode;
        var stderr = GetStderr();
        logger.LogWarning("ACP agent exited: code={Code}, stderr={Stderr}", exitCode, stderr);
        StatusChanged?.Invoke(this, new StatusEvent(false, $"Disconnected (exit {exitCode})"));
        if (stderr is { Length: > 0 })
            EventReceived?.Invoke(this, new ChatErrorEvent($"Agent exited (code {exitCode}):\n{stderr}"));
    }

    private string? GetStderr()
    {
        lock (_stderrBuffer)
            return _stderrBuffer.Length > 0 ? _stderrBuffer.ToString().Trim() : null;
    }

    // ── IAcpClient — session updates ──────────────────────────────────────────

    public Task SessionUpdateAsync(SessionNotification notification, CancellationToken cancellationToken)
    {
        switch (notification.Update)
        {
            case SessionUpdateAgentMessageChunk chunk when chunk.Content is TextContent tc:
                EventReceived?.Invoke(this, new ChatMessageDeltaEvent(tc.Text ?? string.Empty, _currentMessageId));
                break;
            case SessionUpdateAgentThoughtChunk thought when thought.Content is TextContent tc:
                EventReceived?.Invoke(this, new ChatReasoningDeltaEvent(tc.Text ?? string.Empty, _currentMessageId));
                break;
            case SessionUpdateToolCallUpdate toolCall when toolCall.Status == ToolCallStatus.InProgress:
                EventReceived?.Invoke(this, new ChatToolExecutionStartEvent(toolCall.Title ?? toolCall.Kind.ToString()));
                break;
        }
        return Task.CompletedTask;
    }

    // ── IAcpClient — permission ───────────────────────────────────────────────

    public Task<RequestPermissionResponse> RequestPermissionAsync(
        RequestPermissionRequest request, CancellationToken cancellationToken)
    {
        var options = request.Options ?? [];
        var allowOption = options.FirstOrDefault(o => o.Kind is PermissionOptionKind.AllowOnce or PermissionOptionKind.AllowAlways);
        var denyOption = options.FirstOrDefault(o => o.Kind is PermissionOptionKind.RejectOnce or PermissionOptionKind.RejectAlways);
        var allowAlwaysOption = options.FirstOrDefault(o => o.Kind == PermissionOptionKind.AllowAlways);

        var tcs = new TaskCompletionSource<RequestPermissionResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(() =>
            tcs.TrySetResult(new RequestPermissionResponse { Outcome = new RequestPermissionOutcomeCancelled() }));

        var allowCmd = new RelayCommand<Control?>(_ =>
        {
            var id = allowOption?.OptionId ?? (options.Length > 0 ? options[0].OptionId : default);
            tcs.TrySetResult(new RequestPermissionResponse { Outcome = new SelectedPermissionOutcome { OptionId = id } });
        });
        var denyCmd = new RelayCommand<Control?>(_ =>
            tcs.TrySetResult(new RequestPermissionResponse { Outcome = new RequestPermissionOutcomeCancelled() }));
        RelayCommand<Control?>? allowAlwaysCmd = allowAlwaysOption == null ? null :
            new RelayCommand<Control?>(_ =>
                tcs.TrySetResult(new RequestPermissionResponse
                    { Outcome = new SelectedPermissionOutcome { OptionId = allowAlwaysOption.OptionId } }));

        EventReceived?.Invoke(this, new ChatPermissionRequestEvent(
            $"**The agent wants to perform:** {request.ToolCall?.Title ?? "an action"}",
            allowOption?.Name ?? "Allow", denyOption?.Name ?? "Deny",
            allowCmd, denyCmd, allowAlwaysOption?.Name, allowAlwaysCmd));

        return tcs.Task;
    }

    // ── IAcpClient — elicitation ──────────────────────────────────────────────

    public Task<CreateElicitationResponse> CreateAsync(
        CreateElicitationRequest request, CancellationToken cancellationToken)
    {
        logger.LogDebug("ACP elicitation/create: {Message}", request.Message);
        // Decline all elicitations — subclasses can override to show a form UI
        return Task.FromResult<CreateElicitationResponse>(new CreateElicitationResponseDecline());
    }

    public Task CompleteAsync(
        CompleteElicitationNotification notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("ACP elicitation/complete: {Id}", notification.ElicitationId);
        return Task.CompletedTask;
    }

    // ── IAcpClient — file system ──────────────────────────────────────────────

    public async Task<ReadTextFileResponse> ReadTextFileAsync(ReadTextFileRequest request, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(request.Path, cancellationToken).ConfigureAwait(false);
        return new ReadTextFileResponse { Content = content };
    }

    public async Task<WriteTextFileResponse> WriteTextFileAsync(WriteTextFileRequest request, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(request.Path, request.Content, cancellationToken).ConfigureAwait(false);
        return new WriteTextFileResponse();
    }

    // ── IAcpClient — terminal ─────────────────────────────────────────────────

    public Task<CreateTerminalResponse> CreateTerminalAsync(CreateTerminalRequest request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid().ToString("N");
        var buf = new StringBuilder();
        var psi = new ProcessStartInfo(request.Command)
        {
            UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true,
            WorkingDirectory = request.Cwd ?? paths.ProjectsDirectory
        };
        foreach (var arg in request.Args ?? []) psi.ArgumentList.Add(arg);
        foreach (var env in request.Env ?? []) psi.EnvironmentVariables[env.Name] = env.Value;

        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) lock (buf) buf.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (buf) buf.AppendLine(e.Data); };
        proc.Start(); proc.BeginOutputReadLine(); proc.BeginErrorReadLine();

        _terminals[id] = new TerminalEntry(proc, buf);
        return Task.FromResult(new CreateTerminalResponse { TerminalId = id });
    }

    public Task<TerminalOutputResponse> TerminalOutputAsync(TerminalOutputRequest request, CancellationToken cancellationToken)
    {
        if (!_terminals.TryGetValue(request.TerminalId, out var entry))
            return Task.FromResult(new TerminalOutputResponse { Output = string.Empty });
        string output; lock (entry.Output) { output = entry.Output.ToString(); }
        return Task.FromResult(new TerminalOutputResponse
        {
            Output = output, Truncated = false,
            ExitStatus = entry.Process.HasExited ? new TerminalExitStatus { ExitCode = (uint?)entry.Process.ExitCode } : null
        });
    }

    public async Task<WaitForTerminalExitResponse> WaitForTerminalExitAsync(WaitForTerminalExitRequest request, CancellationToken cancellationToken)
    {
        if (!_terminals.TryGetValue(request.TerminalId, out var entry))
            return new WaitForTerminalExitResponse { ExitCode = null };
        await entry.Process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new WaitForTerminalExitResponse { ExitCode = (uint?)entry.Process.ExitCode };
    }

    public Task<ReleaseTerminalResponse> ReleaseTerminalAsync(ReleaseTerminalRequest request, CancellationToken cancellationToken)
    {
        if (_terminals.TryRemove(request.TerminalId, out var entry)) KillTerminalEntry(entry);
        return Task.FromResult(new ReleaseTerminalResponse());
    }

    public Task<KillTerminalResponse> KillTerminalAsync(KillTerminalRequest request, CancellationToken cancellationToken)
    {
        if (_terminals.TryRemove(request.TerminalId, out var entry)) KillTerminalEntry(entry);
        return Task.FromResult(new KillTerminalResponse());
    }

    public Task<object> ExtMethodAsync(string method, object request, CancellationToken cancellationToken)
    {
        logger.LogDebug("ACP unhandled extension method: {Method}", method);
        throw new NotImplementedException($"Extension method '{method}' is not supported.");
    }

    public Task ExtNotificationAsync(string method, object notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("ACP extension notification: {Method}", method);
        return Task.CompletedTask;
    }

    public void OnDisconnected(Connection connection)
    {
        var stderr = GetStderr();
        logger.LogWarning("ACP JSON-RPC disconnected. stderr={Stderr}", stderr);
        StatusChanged?.Invoke(this, new StatusEvent(false, "Disconnected"));
        EventReceived?.Invoke(this, new ChatErrorEvent(
            stderr is { Length: > 0 }
                ? $"Connection lost — agent output:\n{stderr}"
                : "The connection to the agent was lost."));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void KillTerminalEntry(TerminalEntry entry)
    {
        try { if (!entry.Process.HasExited) entry.Process.Kill(entireProcessTree: true); }
        catch { /* ignore */ }
        finally { entry.Process.Dispose(); }
    }

    private sealed record TerminalEntry(Process Process, StringBuilder Output);
}


