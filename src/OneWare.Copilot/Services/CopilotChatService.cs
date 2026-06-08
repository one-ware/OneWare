using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OneWare.Copilot.Models;
using OneWare.Copilot.ViewModels;
using OneWare.Copilot.Views;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Copilot.Services;

public sealed class CopilotChatService(
    ISettingsService settingsService,
    IAiFunctionProvider toolProvider,
    IPackageService packageService,
    IPackageWindowService packageWindowService,
    IWindowService windowService,
    IPaths paths)
    : ObservableObject, IChatServiceWithSessions
{
    private CopilotClient? _client;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private CopilotSession? _session;
    private IDisposable? _subscription;
    private string? _requestedSessionId;

    // Usage tracking
    public long LastInputTokens
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public long LastOutputTokens
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public long? LastReasoningTokens
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public long SessionTotalRequests
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public long SessionTotalInputTokens
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public long SessionTotalOutputTokens
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public long ContextCurrentTokens
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public long ContextTokenLimit
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public double? QuotaRemainingPercent
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool QuotaIsUnlimited
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public DateTimeOffset? QuotaResetDate
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool HasUsageData
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool IsRemoteSession
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string? RemoteSessionUrl
    {
        get;
        private set => SetProperty(ref field, value);
    }

    private static readonly Regex DeviceLoginUrlRegex = new(@"https?://\S+", RegexOptions.Compiled);

    private static readonly Regex DeviceLoginCodeRegex = new(@"\bcode\s+([A-Z0-9\-]+)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ObservableCollection<ModelModel> Models { get; } = [];

    public ModelModel? SelectedModel
    {
        get;
        set
        {
            var oldValue = field;
            if (SetProperty(ref field, value) && value != null)
            {
                settingsService.SetSettingValue(CopilotModule.CopilotSelectedModelSettingKey, value.Id);
                if (oldValue != null && oldValue.Id != value.Id)
                {
                    // When a model is changed we reset the session and force a new one on next init
                    SessionReset?.Invoke(this, EventArgs.Empty);
                    _ = NewChatAsync();
                }
            }
        }
    }

    public string Name { get; } = "Copilot";

    public string? CurrentSessionId
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public Control BottomUiExtension => new CopilotChatExtensionView()
    {
        DataContext = this
    };

    public event EventHandler? SessionReset;
    public event EventHandler<ChatEvent>? EventReceived;
    public event EventHandler<StatusEvent>? StatusChanged;

    private async Task<bool> InstallCopilotCLiAsync(Control? owner, bool update = false)
    {
        if (!update)
        {
            var cliPath = settingsService.GetSettingValue<string>(CopilotModule.CopilotCliSettingKey);
            if (PlatformHelper.ExistsOnPath(cliPath)) return true;
        }

        var installResult = await packageWindowService.QuickInstallPackageAsync(CopilotModule.CopilotPackage.Id!);

        if (!installResult) return false;

        SessionReset?.Invoke(this, EventArgs.Empty);

        await InitializeAsync();
        await AuthenticateAsync(owner);

        return installResult;
    }

    private async Task<bool> AuthenticateAsync(Control? owner)
    {
        if (_client == null) return false;

        try
        {
            bool isAuthenticated;
            try
            {
                var currentAuthStatus = await _client.GetAuthStatusAsync();
                isAuthenticated = currentAuthStatus.IsAuthenticated;
            }
            catch (IOException ex) when (ex.InnerException?.GetType().Name == "RemoteInvocationException" &&
                                         ex.Message.Contains("401"))
            {
                // Treat 401 authentication errors as unauthenticated
                ContainerLocator.Container.Resolve<ILogger>().LogWarning(ex,
                    "Authentication check failed with 401, treating as unauthenticated.");
                isAuthenticated = false;
            }

            if (isAuthenticated) return true;

            var cliPath = settingsService.GetSettingValue<string>(CopilotModule.CopilotCliSettingKey);

            if (!PlatformHelper.ExistsOnPath(cliPath)) return false;

            using var cts = new CancellationTokenSource();
            var viewModel = new CopilotDeviceLoginViewModel(cts);
            var view = new CopilotDeviceLoginView
            {
                DataContext = viewModel
            };

            var ownerWindow = owner != null ? TopLevel.GetTopLevel(owner) as Window : null;
            var showTask = windowService.ShowDialogAsync(view, ownerWindow);
            var loginTask = RunCopilotLoginAsync(cliPath, viewModel, cts.Token);

            var loginResult = await loginTask;

            if (loginResult)
            {
                await Dispatcher.UIThread.InvokeAsync(view.Close);
                await showTask;
                SessionReset?.Invoke(this, EventArgs.Empty);
                return await InitializeAsync();
            }

            if (cts.IsCancellationRequested)
            {
                UpdateLoginStatus(viewModel, "Login cancelled.");
            }
            else
            {
                UpdateLoginStatus(viewModel, "Authentication failed.");
            }

            // Keep dialog lifecycle contained in this method so the token source
            // is not disposed while the window can still trigger cancellation.
            await showTask;
            return false;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().LogError(e.Message, e);
            return false;
        }
    }

    public async Task<bool> InitializeAsync()
    {
        var cliPath = settingsService.GetSettingValue<string>(CopilotModule.CopilotCliSettingKey);


        await _sync.WaitAsync().ConfigureAwait(false);
        await DisposeAsync();

        try
        {
            if (!PlatformHelper.ExistsOnPath(cliPath))
            {
                StatusChanged?.Invoke(this, new StatusEvent(false, "CLI Not found"));
                EventReceived?.Invoke(this, new ChatButtonEvent(
                    "Copilot CLI not found.", "Install Copilot CLI",
                    new AsyncRelayCommand<Control?>(x => InstallCopilotCLiAsync(x))));
                return false;
            }

            if (packageService.IsLoaded || await packageService.RefreshAsync())
            {
                if (packageService.Packages.TryGetValue(CopilotModule.CopilotPackage.Id!, out var state) &&
                    state.Status is PackageStatus.UpdateAvailable)
                {
                    StatusChanged?.Invoke(this, new StatusEvent(false, "CLI Update Available"));
                    EventReceived?.Invoke(this, new ChatButtonEvent(
                        "Copilot CLI update found", "Update Copilot CLI",
                        new AsyncRelayCommand<Control?>(x => InstallCopilotCLiAsync(x, true))));
                    return false;
                }
            }

            _client = new CopilotClient(new CopilotClientOptions()
            {
                WorkingDirectory = paths.ProjectsDirectory,
                Connection = RuntimeConnection.ForStdio(cliPath, []),
                EnableRemoteSessions = true
            });

            bool isAuthenticated;
            try
            {
                var authStatus = await _client.GetAuthStatusAsync();
                isAuthenticated = authStatus.IsAuthenticated;
            }
            catch (IOException ex) when (ex.InnerException?.GetType().Name == "RemoteInvocationException" &&
                                         ex.Message.Contains("401"))
            {
                // Treat 401 authentication errors as unauthenticated
                ContainerLocator.Container.Resolve<ILogger>().LogWarning(ex,
                    "Authentication check failed with 401, treating as unauthenticated.");
                isAuthenticated = false;
            }

            if (!isAuthenticated)
            {
                StatusChanged?.Invoke(this, new StatusEvent(false, "Not Authenticated"));
                EventReceived?.Invoke(this, new ChatButtonEvent(
                    "Not Authenticated to Copilot CLI.", "Login with GitHub",
                    new AsyncRelayCommand<Control?>(AuthenticateAsync)));
                return false;
            }

            StatusChanged?.Invoke(this, new StatusEvent(false, $"Starting Copilot..."));

            await _client.StartAsync();

            var models = await _client.ListModelsAsync();

            StatusChanged?.Invoke(this, new StatusEvent(true, $"Copilot started"));

            Models.Clear();
            Models.AddRange(models.Select(x => new ModelModel()
            {
                Id = x.Id,
                Name = x.Name,
                Billing = $"{x.Billing?.Multiplier}x",
            }).ToArray());

            var selectedModelSetting =
                settingsService.GetSettingValue<string>(CopilotModule.CopilotSelectedModelSettingKey);
            SelectedModel = Models.FirstOrDefault(x => x.Id == selectedModelSetting) ??
                            Models.FirstOrDefault(x => x.Billing == "0x") ?? Models.FirstOrDefault();

            return true;
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, new StatusEvent(false, "Copilot unavailable"));
            EventReceived?.Invoke(this, new ChatErrorEvent(ex.Message));

            return false;
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task InitializeSessionAsync()
    {
        if (_client == null) return;

        await DisposeSessionAsync();

        if (SelectedModel == null)
        {
            EventReceived?.Invoke(this,
                new ChatErrorEvent("No Model Selected"));
            return;
        }

        StatusChanged?.Invoke(this, new StatusEvent(true, $"Connecting to {SelectedModel.Name}..."));

        var sessionId = _requestedSessionId;
        _requestedSessionId = null;

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            var tools = toolProvider.GetTools().Cast<AIFunctionDeclaration>().ToList();
            var sessionConfig = new SessionConfig
            {
                Model = SelectedModel.Id,
                Streaming = true,
                // Only stream root-agent deltas; the chat UI does not differentiate sub-agents.
                IncludeSubAgentStreamingEvents = false,
                SystemMessage = new SystemMessageConfig
                {
                    Content = BuildSystemMessage()
                },
                Tools = tools,
                AvailableTools = tools.Select(x => x.Name).ToList(),
                ClientName = "OneWare Studio",
                OnPermissionRequest = OnPermissionRequestAsync,
                OnUserInputRequest = OnUserInputRequestAsync,
                Hooks = new SessionHooks
                {
                    OnPreToolUse = OnPreToolUseAsync
                }
            };

            _session = await _client.CreateSessionAsync(sessionConfig);
        }
        else
        {
            _session = await _client.ResumeSessionAsync(sessionId, new ResumeSessionConfig()
            {
                Streaming = true,
                IncludeSubAgentStreamingEvents = false,
                Tools = toolProvider.GetTools().Cast<AIFunctionDeclaration>().ToList(),
                OnPermissionRequest = OnPermissionRequestAsync,
                OnUserInputRequest = OnUserInputRequestAsync,
                Hooks = new SessionHooks
                {
                    OnPreToolUse = OnPreToolUseAsync
                }
            });
        }

        CurrentSessionId = _session?.SessionId ?? sessionId;

        StatusChanged?.Invoke(this, new StatusEvent(true, $"Connected"));

        if (_session == null)
        {
            EventReceived?.Invoke(this, new ChatErrorEvent("Failed to initialize Copilot session."));
            return;
        }

        _subscription = _session.On<SessionEvent>(HandleSessionEvent);

        // Enable remote session on-the-fly if the setting is active
        if (settingsService.GetSettingValue<bool>(CopilotModule.CopilotRemoteSessionSettingKey))
        {
            await EnableRemoteSessionAsync();
        }
    }

    private string BuildSystemMessage()
    {
        var additions = toolProvider.GetPromptAdditions();
        if (additions.Count == 0) return CopilotModule.SystemMessage;

        return $"{CopilotModule.SystemMessage}\n\n{string.Join("\n\n", additions)}";
    }

    public async Task SendAsync(string prompt)
    {
        if (SelectedModel == null)
        {
            EventReceived?.Invoke(this,
                new ChatErrorEvent("No Model Selected"));
            return;
        }

        if (_session == null)
        {
            await InitializeSessionAsync();
        }

        if (_session == null) return;
        await _session.SendAsync(new MessageOptions { Prompt = prompt }).ConfigureAwait(false);
    }

    public async Task AbortAsync()
    {
        if (_session == null) return;
        await _session.AbortAsync();
    }

    public async Task NewChatAsync()
    {
        _requestedSessionId = null;
        await InitializeSessionAsync();
    }

    public async Task<bool> LoadSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return false;

        _requestedSessionId = sessionId;

        try
        {
            await InitializeSessionAsync();
            return string.Equals(CurrentSessionId, sessionId, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<ILogger>().LogError(ex, "Failed to load Copilot session {SessionId}.",
                sessionId);
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await DisposeSessionAsync();
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<ILogger>().LogWarning(ex, "Failed to dispose Copilot session.");
        }

        var client = _client;
        _client = null;

        if (client == null) return;

        try
        {
            await client.StopAsync();
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<ILogger>().LogWarning(ex, "Failed to stop Copilot client.");
        }

        try
        {
            await client.DisposeAsync();
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<ILogger>().LogWarning(ex, "Failed to dispose Copilot client.");
        }
    }

    private async Task DisposeSessionAsync()
    {
        _subscription?.Dispose();
        _subscription = null;

        if (_session != null)
        {
            await _session.DisposeAsync();
            _session = null;
        }

        CurrentSessionId = null;
        ResetUsageStats();
    }

    private void ResetUsageStats()
    {
        LastInputTokens = 0;
        LastOutputTokens = 0;
        LastReasoningTokens = null;
        SessionTotalRequests = 0;
        SessionTotalInputTokens = 0;
        SessionTotalOutputTokens = 0;
        ContextCurrentTokens = 0;
        ContextTokenLimit = 0;
        QuotaRemainingPercent = null;
        QuotaIsUnlimited = false;
        QuotaResetDate = null;
        HasUsageData = false;
    }

    private void HandleSessionEvent(SessionEvent evt)
    {
        switch (evt)
        {
            case AssistantMessageDeltaEvent x:
            {
                EventReceived?.Invoke(this,
                    new ChatMessageDeltaEvent(x.Data.DeltaContent, x.Data.MessageId));
                break;
            }
            case AssistantMessageEvent x:
            {
                EventReceived?.Invoke(this,
                    new ChatMessageEvent(x.Data.Content, x.Data.MessageId));
                break;
            }
            case AssistantReasoningDeltaEvent x:
            {
                EventReceived?.Invoke(this,
                    new ChatReasoningDeltaEvent(x.Data.DeltaContent, x.Data.ReasoningId));
                break;
            }
            case AssistantReasoningEvent x:
            {
                EventReceived?.Invoke(this,
                    new ChatReasoningEvent(x.Data.Content, x.Data.ReasoningId));
                break;
            }
            case UserMessageEvent x:
            {
                EventReceived?.Invoke(this,
                    new ChatUserMessageEvent(x.Data.Content));
                break;
            }
            case ToolExecutionStartEvent x:
            {
                EventReceived?.Invoke(this,
                    new ChatToolExecutionStartEvent(x.Data.ToolName));
                break;
            }
            case SessionErrorEvent error:
                EventReceived?.Invoke(this,
                    new ChatErrorEvent(error.Data.Message));
                break;
            case SessionIdleEvent:
                EventReceived?.Invoke(this, new ChatIdleEvent());
                break;
            case AssistantUsageEvent usage:
                UpdateUsageFromAssistantEvent(usage.Data);
                break;
            case SessionUsageInfoEvent info:
                ContextCurrentTokens = info.Data.CurrentTokens;
                ContextTokenLimit = info.Data.TokenLimit;
                break;
            case SessionInfoEvent sessionInfo when sessionInfo.Data.InfoType == "remote"
                                                   && !string.IsNullOrWhiteSpace(sessionInfo.Data.Url):
                RemoteSessionUrl = sessionInfo.Data.Url;
                IsRemoteSession = true;
                break;
        }
    }

    private void UpdateUsageFromAssistantEvent(AssistantUsageData data)
    {
        LastInputTokens = data.InputTokens ?? 0;
        LastOutputTokens = data.OutputTokens ?? 0;
        LastReasoningTokens = data.ReasoningTokens is > 0 ? data.ReasoningTokens : null;
        SessionTotalRequests++;
        SessionTotalInputTokens += data.InputTokens ?? 0;
        SessionTotalOutputTokens += data.OutputTokens ?? 0;

        HasUsageData = true;
    }

    // ── Remote session ────────────────────────────────────────────────────────

    public async Task EnableRemoteSessionAsync()
    {
        if (_session == null) return;
        try
        {
            var result = await _session.Rpc.Remote.EnableAsync();
            RemoteSessionUrl = result.Url;
            IsRemoteSession = !string.IsNullOrWhiteSpace(result.Url);
            if (IsRemoteSession)
                EventReceived?.Invoke(this, new ChatMessageEvent($"Remote session active: {result.Url}"));
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<ILogger>().LogWarning(ex, "Failed to enable remote session.");
        }
    }

    public async Task DisableRemoteSessionAsync()
    {
        if (_session == null) return;
        try
        {
            await _session.Rpc.Remote.DisableAsync();
            IsRemoteSession = false;
            RemoteSessionUrl = null;
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<ILogger>().LogWarning(ex, "Failed to disable remote session.");
        }
    }

    // ── OnPreToolUse — returns "ask" to escalate to OnPermissionRequest ────────

    private Task<PreToolUseHookOutput?> OnPreToolUseAsync(PreToolUseHookInput input, HookInvocation invocation)
    {
        var check = toolProvider.GetConfirmationCheck(input.ToolName);
        if (check == null)
            return Task.FromResult<PreToolUseHookOutput?>(new PreToolUseHookOutput { PermissionDecision = "allow" });

        // ToolArgs can arrive as a JSON object or as a JSON-encoded string containing an object
        var rawDict = new Dictionary<string, object?>();
        var toolArgs = input.ToolArgs;
        if (toolArgs is { ValueKind: JsonValueKind.String } strEl)
        {
            var jsonStr = strEl.GetString();
            if (!string.IsNullOrWhiteSpace(jsonStr))
                toolArgs = JsonDocument.Parse(jsonStr).RootElement;
        }
        if (toolArgs is { ValueKind: JsonValueKind.Object } objEl)
            foreach (var prop in objEl.EnumerateObject())
                rawDict[prop.Name] = prop.Value;

        var reason = check(new AIFunctionArguments(rawDict));

        return reason == null
            ? Task.FromResult<PreToolUseHookOutput?>(new PreToolUseHookOutput { PermissionDecision = "allow" })
            : Task.FromResult<PreToolUseHookOutput?>(new PreToolUseHookOutput { PermissionDecision = "ask", PermissionDecisionReason = reason });
    }

    // OnPermissionRequest — single place for all permission dialogs

    private Task<PermissionDecision> OnPermissionRequestAsync(
        PermissionRequest request,
        PermissionInvocation invocation)
    {
        if (request is PermissionRequestCustomTool)
            return Task.FromResult(PermissionDecision.ApproveOnce());
        
        var message = BuildPermissionMessage(request);

        var responseSource = new TaskCompletionSource<PermissionDecision>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var allowCommand =
            new RelayCommand<Control?>(_ => responseSource.TrySetResult(PermissionDecision.ApproveOnce()));
        var denyCommand =
            new RelayCommand<Control?>(_ => responseSource.TrySetResult(PermissionDecision.Reject("Denied by User")));
        var allowForSessionCmd = new RelayCommand<Control?>(_ =>
        {
            responseSource.TrySetResult(new PermissionDecisionApproveForSession());
        });

        EventReceived?.Invoke(this, new ChatPermissionRequestEvent(
            message, "Allow", "Deny", allowCommand, denyCommand, "Allow for session", allowForSessionCmd));

        return responseSource.Task;
    }

    private static string BuildPermissionMessage(PermissionRequest request) => request switch
    {
        PermissionRequestHook { HookMessage: { Length: > 0 } msg } => msg,
        PermissionRequestHook hook => $"**Copilot wants to run `{hook.ToolName}`.**",
        PermissionRequestShell shell => BuildShellMessage(shell),
        PermissionRequestWrite write => $"**Copilot wants to edit a file.**\n\n`{write.FileName}`",
        PermissionRequestRead read => $"**Copilot wants to read a file.**\n\n`{read.Path}`",
        PermissionRequestUrl url => $"**Copilot wants to open a URL.**\n\n`{url.Url}`",
        PermissionRequestMcp mcp =>
            $"**Copilot wants to invoke an MCP tool.**\n\nServer: `{mcp.ServerName}`, Tool: `{mcp.ToolName}`",
        PermissionRequestMemory mem => $"**Copilot wants to update its memory.**\n\nSubject: `{mem.Subject}`",
        _ => $"**Copilot wants to use a tool.**\n\nKind: `{request.Kind ?? "unknown"}`"
    };

    private static string BuildShellMessage(PermissionRequestShell shell)
    {
        var msg = $"**Copilot wants to execute a command in the terminal.**\n\n```\n{shell.FullCommandText}\n```";
        if (!string.IsNullOrWhiteSpace(shell.Warning))
            msg += $"\n\n⚠️ {shell.Warning}";
        return msg;
    }

    private Task<UserInputResponse> OnUserInputRequestAsync(
        UserInputRequest request,
        UserInputInvocation invocation)
    {
        var message = $"Copilot requested user input: {request.Question}";
        EventReceived?.Invoke(this, new ChatMessageEvent(message));

        return Task.FromResult(new UserInputResponse
        {
            Answer = string.Empty,
            WasFreeform = true
        });
    }

    private async Task<bool> RunCopilotLoginAsync(string cliPath, CopilotDeviceLoginViewModel viewModel,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(cliPath, "login")
        {
            WorkingDirectory = paths.ProjectsDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = startInfo;
        process.EnableRaisingEvents = true;

        try
        {
            if (!process.Start())
            {
                UpdateLoginStatus(viewModel, "Failed to start Copilot CLI.");
                return false;
            }
        }
        catch (Exception ex)
        {
            UpdateLoginStatus(viewModel, $"Failed to start Copilot CLI: {ex.Message}");
            return false;
        }

        UpdateLoginStatus(viewModel, "Waiting for device code...");

        var stdoutTask = ReadLoginStreamAsync(process.StandardOutput, viewModel, cancellationToken);
        var stderrTask = ReadLoginStreamAsync(process.StandardError, viewModel, cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            return false;
        }

        await Task.WhenAll(stdoutTask, stderrTask);

        if (process.ExitCode != 0)
        {
            UpdateLoginStatus(viewModel, $"Copilot CLI exited with code {process.ExitCode}.");
            return false;
        }

        return true;
    }

    private async Task ReadLoginStreamAsync(StreamReader reader, CopilotDeviceLoginViewModel viewModel,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;
            ApplyLoginOutputLine(viewModel, line);
        }
    }

    private void ApplyLoginOutputLine(CopilotDeviceLoginViewModel viewModel, string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        UpdateLoginViewModel(viewModel, () =>
        {
            var urlMatch = DeviceLoginUrlRegex.Match(line);
            if (urlMatch.Success && string.IsNullOrWhiteSpace(viewModel.VerificationUrl))
            {
                viewModel.VerificationUrl = urlMatch.Value;
            }

            var codeMatch = DeviceLoginCodeRegex.Match(line);
            if (codeMatch.Success && string.IsNullOrWhiteSpace(viewModel.UserCode))
            {
                viewModel.UserCode = codeMatch.Groups[1].Value.ToUpperInvariant();
            }

            if (line.Contains("Waiting for authorization", StringComparison.OrdinalIgnoreCase))
            {
                viewModel.StatusText = "Waiting for authorization...";
            }
            else if (line.Contains("To authenticate", StringComparison.OrdinalIgnoreCase))
            {
                viewModel.StatusText = "Enter the code in your browser.";
            }
        });
    }

    private static void UpdateLoginStatus(CopilotDeviceLoginViewModel viewModel, string status)
    {
        UpdateLoginViewModel(viewModel, () => viewModel.StatusText = status);
    }

    private static void UpdateLoginViewModel(CopilotDeviceLoginViewModel viewModel, Action update)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            update();
        }
        else
        {
            Dispatcher.UIThread.Post(update);
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(true);
        }
        catch
        {
        }
    }
}