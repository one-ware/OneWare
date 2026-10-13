using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Chat.ViewModels.ChatMessages;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Chat.ViewModels;

/// <summary>Callbacks a chat needs from the chat panel that owns it.</summary>
internal interface IChatSessionHost
{
    /// <summary>Schedules a throttled save of the chat's transcript.</summary>
    void RequestSave(ChatSessionViewModel session);

    /// <summary>Saves all chats immediately, e.g. after a turn completed.</summary>
    void SaveNow();

    /// <summary>Stores the current transcript of the chat in the history before it changes.</summary>
    void StoreTranscript(ChatSessionViewModel session);

    /// <summary>The user sends a message from this chat, e.g. from the overview to start it.</summary>
    void OnSending(ChatSessionViewModel session);

    /// <summary>The service switched the chat to another session (new chat, replacement or reset).</summary>
    void OnSessionChanged(ChatSessionViewModel session);
}

/// <summary>
/// One chat: the conversation with one session of a chat service. Chats run independently, so a session keeps
/// working while another chat or the overview is shown.
/// </summary>
public partial class ChatSessionViewModel : ObservableObject
{
    public const string DefaultTitle = "New chat";

    private const string DefaultWorkingStatus = "Working…";

    private static readonly TimeSpan ConnectionWaitTimeout = TimeSpan.FromMinutes(2);

    private readonly IChatSessionHost _host;
    private readonly IAiFunctionProvider _aiFunctionProvider;
    private ChatMessageErrorViewModel? _notConnectedMessage;

    // Set while the conversation shows errors or service prompts that a successful answer resolves.
    private bool _hasResolvableMessages;

    /// <summary>
    /// Assistant messages of the running turn, in order, outside of sub-agent blocks. Only these may
    /// be labelled with the model when the turn ends.
    /// </summary>
    private List<ChatMessageAssistantViewModel> _turnAssistantMessages = [];

    private readonly Dictionary<string, ChatMessageAssistantViewModel> _assistantMessagesById =
        new(StringComparer.Ordinal);

    private readonly Dictionary<string, ChatMessageReasoningViewModel> _assistantReasoningById =
        new(StringComparer.Ordinal);

    /// <summary>Sub-agent blocks by id, for as long as their events can still arrive.</summary>
    private readonly Dictionary<string, ChatMessageSubAgentViewModel> _subAgents = new(StringComparer.Ordinal);

    /// <summary>
    /// Tool calls a sub-agent started that OneWare executes itself, keyed by the tool call id. They
    /// are reported twice — once by the chat service (with the sub-agent it belongs to) and once by
    /// the function provider (with the live output and a stop button) — and are matched up here so
    /// the running tool is shown inside its sub-agent block instead of the main flow.
    /// </summary>
    private readonly Dictionary<string, ChatMessageSubAgentViewModel> _pendingSubAgentTools =
        new(StringComparer.Ordinal);

    private Task<bool>? _activationTask;

    // Single-flight guard: a send that happens while initialization is still running must join
    // the running operation instead of starting a second (destructive) initialization.
    private Task<bool>? _initializeTask;
    private IChatService? _initializeTaskService;

    // Completion sources waiting for the service to report a connected status.
    private readonly List<TaskCompletionSource<bool>> _connectionWaiters = [];

    // FIFO of messages sent locally so the echoed ChatUserMessageEvent can be matched
    // (suppressed for normal/steered sends, or used to activate a queued message).
    private readonly Queue<PendingLocalMessage> _pendingLocalMessages = new();
    private readonly List<CancelledQueuedMessage> _cancelledQueuedMessages = [];

    // What the agent is doing during the current turn, shown next to the busy indicator.
    private readonly ChatActivityStatus _activity = new();

    private sealed record PendingLocalMessage(
        string Content,
        ChatSendMode Mode,
        ChatMessageUserViewModel? QueuedView);

    private sealed record CancelledQueuedMessage(string Content, DateTimeOffset ExpiresAt);

    internal ChatSessionViewModel(IChatService registeredService, IChatService service, IChatSessionHost host,
        IAiFunctionProvider aiFunctionProvider, IChatAgentService agentService)
    {
        RegisteredService = registeredService;
        Service = service;
        _host = host;
        _aiFunctionProvider = aiFunctionProvider;
        AgentService = agentService;

        SendCommand = new AsyncRelayCommand(() => SendInternalAsync(ChatSendMode.Send), CanSend);
        SteerCommand = new AsyncRelayCommand(() => SendInternalAsync(ChatSendMode.Steer), CanSteerOrQueue);
        QueueCommand = new AsyncRelayCommand(() => SendInternalAsync(ChatSendMode.Queue), CanSteerOrQueue);
        AbortCommand = new AsyncRelayCommand(AbortAsync, CanAbort);
        NewChatCommand = new AsyncRelayCommand(NewChatAsync);
        RemoveQueuedMessageCommand =
            new AsyncRelayCommand<ChatMessageUserViewModel>(RemoveQueuedMessageAsync, CanRemoveQueuedMessage);
        InitializeCurrentCommand = new AsyncRelayCommand(InitializeCurrentAsync);

        QueuedMessages.CollectionChanged += (_, _) => RemoveQueuedMessageCommand.NotifyCanExecuteChanged();
        Messages.CollectionChanged += (_, _) => RequestSaveState();

        Blocker = service.Blocker;
        service.EventReceived += OnEventReceived;
        service.StatusChanged += OnStatusChanged;
        service.SessionReset += OnSessionReset;
        service.PropertyChanged += OnChatServicePropertyChanged;
    }

    /// <summary>The service as registered with the chat panel, e.g. for the service selector.</summary>
    public IChatService RegisteredService { get; }

    /// <summary>
    /// The service instance this chat talks to: <see cref="RegisteredService" /> itself, or a parallel session of it.
    /// </summary>
    public IChatService Service { get; }

    public IChatAgentService AgentService { get; }

    public string Title
    {
        get;
        set => SetProperty(ref field, value);
    } = DefaultTitle;

    /// <summary>When the chat was opened; orders new chats on the overview before they are stored.</summary>
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.Now;

    /// <summary>Whether the chat is the one shown in the chat panel.</summary>
    public bool IsSelected
    {
        get;
        set
        {
            if (SetProperty(ref field, value) && value) HasAttention = false;
        }
    }

    /// <summary>Set when the chat finished a turn or waits for the user while it is not shown.</summary>
    public bool HasAttention
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    /// The session this chat shows. Stays set while the service is (re)connecting, so the session can be resumed.
    /// </summary>
    public string? RememberedSessionId { get; set; }

    /// <summary>Session the service currently runs for this chat.</summary>
    public string? CurrentSessionId => (Service as IChatServiceWithSessions)?.CurrentSessionId;

    /// <summary>Set when the transcript changed since it was last saved.</summary>
    internal bool IsDirty { get; set; }

    public AsyncRelayCommand NewChatCommand { get; }

    public AsyncRelayCommand SendCommand { get; }

    public AsyncRelayCommand SteerCommand { get; }

    public AsyncRelayCommand QueueCommand { get; }

    public AsyncRelayCommand AbortCommand { get; }

    public AsyncRelayCommand<ChatMessageUserViewModel> RemoveQueuedMessageCommand { get; }

    public AsyncRelayCommand InitializeCurrentCommand { get; }

    /// <summary>
    /// Set while the service cannot be used (e.g. login or subscription required). The chat is hidden
    /// and replaced by a panel describing the blocker until the service clears it.
    /// </summary>
    public ChatServiceBlocker? Blocker
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsBlocked));
                OnPropertyChanged(nameof(HasConnectionProblem));
            }
        }
    }

    public bool IsBlocked => Blocker != null;

    private void OnChatServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IChatService.Blocker)) return;

        Dispatcher.UIThread.Post(() => Blocker = Service.Blocker);
    }

    private void MarkAttentionIfHidden()
    {
        if (!IsSelected) HasAttention = true;
    }

    /// <summary>Set once the chat connected its service and resumed its session.</summary>
    internal bool IsActivated { get; private set; }

    /// <summary>
    /// Connects the service and resumes <see cref="RememberedSessionId" /> the first time the chat is used.
    /// </summary>
    internal Task<bool> EnsureActivatedAsync()
    {
        if (IsActivated) return IsInitialized ? Task.FromResult(true) : InitializeCurrentAsync();
        if (_activationTask != null) return _activationTask;

        var task = ActivateAsync();
        if (!task.IsCompleted) _activationTask = task;
        return task;
    }

    private async Task<bool> ActivateAsync()
    {
        try
        {
            var initialized = await InitializeCurrentAsync();
            if (!initialized) return false;

            if (Service is IChatServiceWithSessions sessions)
            {
                var targetSessionId = RememberedSessionId;
                if (!string.IsNullOrWhiteSpace(targetSessionId))
                {
                    if (!string.Equals(sessions.CurrentSessionId, targetSessionId, StringComparison.Ordinal))
                        await sessions.LoadSessionAsync(targetSessionId);
                    IsActivated = true;
                    ShowReplacementSessionIfNeeded(targetSessionId);
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(sessions.CurrentSessionId))
                {
                    // The service instance still holds the session of a chat that was unloaded.
                    await Service.NewChatAsync();
                }
            }

            IsActivated = true;
            _host.OnSessionChanged(this);
            return true;
        }
        finally
        {
            _activationTask = null;
        }
    }

    /// <summary>Shows a stored chat in this chat and resumes its session.</summary>
    internal async Task LoadSessionAsync(string sessionId, IReadOnlyCollection<ChatMessageState>? states, string? title)
    {
        if (Service is not IChatServiceWithSessions sessions) return;
        if (string.Equals(CurrentSessionId, sessionId, StringComparison.Ordinal)) return;

        _host.StoreTranscript(this);
        if (IsBusy) await AbortAsync();

        RememberedSessionId = sessionId;
        LoadMessagesFromStates(states ?? []);
        Title = string.IsNullOrWhiteSpace(title) ? DefaultTitle : title;
        IsDirty = false;

        if (!await EnsureActivatedAsync()) return;
        if (string.Equals(sessions.CurrentSessionId, sessionId, StringComparison.Ordinal))
        {
            NotifyContentAdded();
            return;
        }

        var loaded = await sessions.LoadSessionAsync(sessionId);
        if (!loaded)
        {
            ShowReplacementSessionIfNeeded(sessionId);
            AddErrorMessage($"Failed to load session '{sessionId}'.");
            return;
        }

        _host.OnSessionChanged(this);
        NotifyContentAdded();
    }

    // Triggers a scroll to bottom
    public event EventHandler? ContentAdded;

    public string CurrentMessage
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                SendCommand.NotifyCanExecuteChanged();
                SteerCommand.NotifyCanExecuteChanged();
                QueueCommand.NotifyCanExecuteChanged();
            }
        }
    } = string.Empty;

    public bool IsBusy
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                SendCommand.NotifyCanExecuteChanged();
                SteerCommand.NotifyCanExecuteChanged();
                QueueCommand.NotifyCanExecuteChanged();
                AbortCommand.NotifyCanExecuteChanged();
                OnBusyChanged(value);
                OnPropertyChanged(nameof(IsActivityVisible));
            }
        }
    }

    public bool IsInitialized
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsConnected
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                SendCommand.NotifyCanExecuteChanged();
                SteerCommand.NotifyCanExecuteChanged();
                QueueCommand.NotifyCanExecuteChanged();
                AbortCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(HasConnectionProblem));

                if (value) ReleaseConnectionWaiters(true);
            }
        }
    }

    /// <summary>
    /// True while a send is waiting for the selected service to finish connecting.
    /// </summary>
    public bool IsWaitingForConnection
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                SendCommand.NotifyCanExecuteChanged();
                SteerCommand.NotifyCanExecuteChanged();
                QueueCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(IsActivityVisible));
                UpdateWorkingStatus();
            }
        }
    }

    /// <summary>Whether the activity indicator below the conversation is shown.</summary>
    public bool IsActivityVisible => IsBusy || IsWaitingForConnection;

    /// <summary>
    /// True while the selected service is still initializing/starting up. Sending is allowed in
    /// this state; the message is held back until the connection is up.
    /// </summary>
    public bool IsConnecting
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                SendCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(HasConnectionProblem));
            }
        }
    }

    /// <summary>Set once the service finished its first connection attempt for this chat.</summary>
    private bool ConnectionAttempted
    {
        get;
        set
        {
            if (SetProperty(ref field, value)) OnPropertyChanged(nameof(HasConnectionProblem));
        }
    }

    /// <summary>
    /// True when the service could not connect (or lost its connection) and isn't retrying on its own;
    /// the chat then offers to retry with the service's status as the reason.
    /// </summary>
    public bool HasConnectionProblem => ConnectionAttempted && !IsConnecting && !IsConnected && !IsBlocked;

    public string StatusText
    {
        get;
        set
        {
            if (SetProperty(ref field, value) && IsWaitingForConnection) UpdateWorkingStatus();
        }
    } = "Starting...";

    public ObservableCollection<IChatMessage> Messages { get; set; } = new();

    /// <summary>
    /// Messages the user has queued while the agent is busy. Rendered (dimmed) below the
    /// conversation until the agent activates them.
    /// </summary>
    public ObservableCollection<IChatMessage> QueuedMessages { get; } = new();

    /// <summary>
    /// Text of the activity indicator: the connection status while a send waits for the service, else
    /// what the agent is doing (thinking, writing, running a tool, waiting for approval, steering, ...)
    /// and how long the turn has been running.
    /// </summary>
    public string WorkingStatusText
    {
        get;
        private set => SetProperty(ref field, value);
    } = DefaultWorkingStatus;

    /// <summary>Whether the current planning turn already offered how to continue.</summary>
    private bool _planOptionsOffered;

    private async Task<bool> InitializeCurrentAsync()
    {
        var service = Service;
        if (service == null) return false;

        // Join an initialization that is already running for this service instead of
        // starting a second one (a concurrent initialization tears down the client the
        // first one is still setting up).
        if (_initializeTask is { } running && _initializeTaskService == service)
        {
            return await running;
        }

        // Prompts and errors from an earlier attempt (e.g. "Upgrade to Pro") are stale now; the
        // service reports them again if the problem persists.
        DismissResolvedMessages();

        var task = InitializeServiceAsync(service);
        _initializeTask = task;
        _initializeTaskService = service;
        IsConnecting = true;

        try
        {
            return await task;
        }
        finally
        {
            if (ReferenceEquals(_initializeTask, task))
            {
                _initializeTask = null;
                _initializeTaskService = null;
                IsConnecting = false;
            }
        }
    }

    private async Task<bool> InitializeServiceAsync(IChatService service)
    {
        var status = await service.InitializeAsync();

        if (Service != service) return status;

        IsInitialized = status;
        ConnectionAttempted = true;

        if (!status) ReleaseConnectionWaiters(false);

        return status;
    }

    /// <summary>
    /// Waits until the selected service reports a connected status, instead of failing a send
    /// that arrives while the service is still starting up.
    /// </summary>
    private async Task<bool> WaitForConnectionAsync()
    {
        if (IsConnected) return true;

        var waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _connectionWaiters.Add(waiter);

        IsWaitingForConnection = true;

        try
        {
            return await waiter.Task.WaitAsync(ConnectionWaitTimeout);
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            _connectionWaiters.Remove(waiter);
            IsWaitingForConnection = _connectionWaiters.Count > 0;
        }
    }

    private void ReleaseConnectionWaiters(bool connected)
    {
        if (_connectionWaiters.Count == 0) return;

        foreach (var waiter in _connectionWaiters.ToArray())
        {
            waiter.TrySetResult(connected);
        }
    }

    /// <summary>
    /// A session that no longer exists in the backend is replaced by a new one; the transcript of
    /// the requested session must not stay visible, otherwise it would be saved into the new session.
    /// </summary>
    private void ShowReplacementSessionIfNeeded(string requestedSessionId)
    {
        if (Service is not IChatServiceWithSessions sessions ||
            string.IsNullOrWhiteSpace(sessions.CurrentSessionId) ||
            string.Equals(sessions.CurrentSessionId, requestedSessionId, StringComparison.Ordinal))
        {
            return;
        }

        Messages.Clear();
        _assistantMessagesById.Clear();
        _turnAssistantMessages.Clear();
        _assistantReasoningById.Clear();
        _subAgents.Clear();
        _pendingSubAgentTools.Clear();

        _host.OnSessionChanged(this);
    }

    private async Task NewChatAsync()
    {
        // Agent files may have been added or edited since the last chat started.
        AgentService.Refresh();

        _host.StoreTranscript(this);

        if (!IsInitialized)
        {
            await EnsureActivatedAsync();
        }

        await AbortAsync();
        await Service.NewChatAsync();

        RememberedSessionId = null;
        Title = DefaultTitle;
        _host.OnSessionChanged(this);

        Messages.Clear();
        QueuedMessages.Clear();
        _pendingLocalMessages.Clear();
        _cancelledQueuedMessages.Clear();
        ResetActivity();
        _assistantMessagesById.Clear();
        _turnAssistantMessages.Clear();
        _assistantReasoningById.Clear();
        _notConnectedMessage = null;
    }

    private async Task SendInternalAsync(ChatSendMode mode)
    {
        var prompt = CurrentMessage.Trim();
        if (string.IsNullOrWhiteSpace(prompt)) return;

        _host.OnSending(this);

        // The next planning turn gets its own decision block. Steering or queueing happens inside a
        // running turn and must not bring the offer of the current one back.
        if (mode == ChatSendMode.Send) _planOptionsOffered = false;

        var chatService = Service;
        if (chatService == null)
        {
            AddErrorMessage("No chat service selected.");
            return;
        }

        var initialized = IsInitialized;
        if (!initialized)
        {
            initialized = await EnsureActivatedAsync();
        }

        if (!IsConnected)
        {
            // The service may still be starting up (typical right after IDE launch).
            // Wait for it to connect instead of rejecting the message.
            var connected = initialized && await WaitForConnectionAsync();

            if (!connected)
            {
                // Replace (don't stack) the transient connection warning; it is removed
                // again as soon as a message actually goes through.
                if (_notConnectedMessage != null) Messages.Remove(_notConnectedMessage);
                _notConnectedMessage =
                    new ChatMessageErrorViewModel($"{chatService.Name} is not connected yet.");
                AddMessage(_notConnectedMessage);
                _hasResolvableMessages = true;
                return;
            }
        }

        // The user may have switched services while we were waiting for the connection.
        if (!ReferenceEquals(Service, chatService)) return;

        if (_notConnectedMessage != null)
        {
            Messages.Remove(_notConnectedMessage);
            _notConnectedMessage = null;
        }

        if (!Messages.OfType<ChatMessageUserViewModel>().Any()) Title = ChatTranscript.BuildChatName(prompt);

        var userMessage = new ChatMessageUserViewModel(prompt);
        ChatMessageAssistantViewModel? assistantMessage = null;

        switch (mode)
        {
            case ChatSendMode.Queue:
                // Park the message (dimmed) below the conversation until the agent activates it.
                QueuedMessages.Add(userMessage);
                _pendingLocalMessages.Enqueue(new PendingLocalMessage(prompt, mode, userMessage));
                break;

            case ChatSendMode.Steer:
                AddMessage(userMessage);
                _pendingLocalMessages.Enqueue(new PendingLocalMessage(prompt, mode, null));
                _activity.IsSteering = true;
                UpdateWorkingStatus();
                break;

            default:
                AddMessage(userMessage);
                _pendingLocalMessages.Enqueue(new PendingLocalMessage(prompt, mode, null));
                // Only the initial (idle) send shows a placeholder; steered messages join the
                // turn that is already streaming.
                assistantMessage = new ChatMessageAssistantViewModel("init") { IsStreaming = true };
                AddMessage(assistantMessage);
                break;
        }

        CurrentMessage = string.Empty;
        IsBusy = true;

        NotifyContentAdded();

        try
        {
            await chatService.SendAsync(prompt, mode);
        }
        catch (Exception ex)
        {
            if (mode == ChatSendMode.Queue)
            {
                QueuedMessages.Remove(userMessage);
                RemovePendingLocalMessage(userMessage);
            }
            else if (Messages.LastOrDefault() is ChatMessageAssistantViewModel { MessageId: "init" } initMessage)
            {
                Messages.Remove(initMessage);
            }
            else if (assistantMessage != null)
            {
                Messages.Remove(assistantMessage);
            }

            AddErrorMessage(ex.Message);
            if (mode == ChatSendMode.Send) IsBusy = false;
            if (mode == ChatSendMode.Steer)
            {
                _activity.IsSteering = false;
                UpdateWorkingStatus();
            }
        }
    }

    private bool CanSteerOrQueue() =>
        IsConnected && IsBusy && !string.IsNullOrWhiteSpace(CurrentMessage);

    private async Task AbortAsync()
    {
        if (Service == null) return;

        var queueCleared = QueuedMessages.Count == 0;
        if (!queueCleared)
        {
            try
            {
                queueCleared = await Service.ClearQueuedMessagesAsync();
            }
            catch (Exception ex)
            {
                AddErrorMessage($"Failed to clear queued messages: {ex.Message}");
            }
        }

        try
        {
            await Service.AbortAsync();
        }
        finally
        {
            if (queueCleared)
                CancelQueuedMessagesLocally();

            RemoveOpenRequests();

            IsBusy = !queueCleared && QueuedMessages.Count > 0;
        }
    }

    /// <summary>Removes questions and permission prompts the stopped turn no longer waits for.</summary>
    private void RemoveOpenRequests()
    {
        foreach (var message in Messages
                     .Where(m => m is ChatMessageUserInputRequestViewModel { IsAnswered: false }
                         or ChatMessagePermissionRequestViewModel)
                     .ToList())
            Messages.Remove(message);
    }

    private async Task RemoveQueuedMessageAsync(ChatMessageUserViewModel? message)
    {
        if (message == null || Service == null || !CanRemoveQueuedMessage(message)) return;

        bool removed;
        try
        {
            removed = await Service.RemoveMostRecentQueuedMessageAsync();
        }
        catch (Exception ex)
        {
            AddErrorMessage($"Failed to remove queued message: {ex.Message}");
            return;
        }

        if (!removed || !QueuedMessages.Contains(message)) return;

        QueuedMessages.Remove(message);
        RemovePendingLocalMessage(message);
    }

    private bool CanRemoveQueuedMessage(ChatMessageUserViewModel? message) =>
        message != null && ReferenceEquals(QueuedMessages.LastOrDefault(), message);

    private bool CanSend() => (IsConnected || IsConnecting) && !IsWaitingForConnection &&
                              !string.IsNullOrWhiteSpace(CurrentMessage);

    private bool CanAbort() => IsConnected && IsBusy;

    private bool TryDequeuePendingLocal(string content, out PendingLocalMessage pending)
    {
        if (_pendingLocalMessages.Count > 0 && _pendingLocalMessages.Peek().Content == content)
        {
            pending = _pendingLocalMessages.Dequeue();
            return true;
        }

        pending = default!;
        return false;
    }

    private bool TryDiscardCancelledQueuedMessage(string content)
    {
        var now = DateTimeOffset.Now;
        _cancelledQueuedMessages.RemoveAll(x => x.ExpiresAt <= now);

        var index = _cancelledQueuedMessages.FindIndex(x =>
            string.Equals(x.Content, content, StringComparison.Ordinal));
        if (index < 0) return false;

        _cancelledQueuedMessages.RemoveAt(index);
        return true;
    }

    private void CancelQueuedMessagesLocally()
    {
        var expiresAt = DateTimeOffset.Now.AddSeconds(30);
        foreach (var pending in _pendingLocalMessages.Where(x => x.Mode == ChatSendMode.Queue))
            _cancelledQueuedMessages.Add(new CancelledQueuedMessage(pending.Content, expiresAt));

        var remaining = _pendingLocalMessages.Where(x => x.Mode != ChatSendMode.Queue).ToArray();
        _pendingLocalMessages.Clear();
        foreach (var pending in remaining)
            _pendingLocalMessages.Enqueue(pending);

        QueuedMessages.Clear();
    }

    private void RemovePendingLocalMessage(ChatMessageUserViewModel message)
    {
        var remaining = _pendingLocalMessages
            .Where(x => !ReferenceEquals(x.QueuedView, message))
            .ToArray();
        _pendingLocalMessages.Clear();
        foreach (var pending in remaining)
            _pendingLocalMessages.Enqueue(pending);
    }

    private void AddMessage(IChatMessage message)
    {
        if (Messages.LastOrDefault() is ChatMessageAssistantViewModel { MessageId: "init" } initMessage)
        {
            Messages.Remove(initMessage);
        }

        Messages.Add(message);
    }

    /// <summary>
    /// Adds a message to the conversation, or into the block of the sub-agent it belongs to.
    /// </summary>
    private void AddMessage(IChatMessage message, string? agentId)
    {
        if (agentId != null && _subAgents.TryGetValue(agentId, out var subAgent))
        {
            subAgent.Items.Add(message);
            RequestSaveState();
            return;
        }

        AddMessage(message);
    }

    private void AddErrorMessage(string? message, string? agentId = null)
    {
        var errorMessage = string.IsNullOrWhiteSpace(message)
            ? "An unexpected error occurred."
            : message;

        AddMessage(new ChatMessageErrorViewModel(errorMessage), agentId);
        _hasResolvableMessages = true;
    }

    /// <summary>
    /// Removes earlier errors and service prompts (e.g. "Upgrade to Pro") from the conversation once the
    /// main agent answers again, since the problem they reported is resolved.
    /// </summary>
    private void DismissResolvedMessages()
    {
        if (!_hasResolvableMessages) return;
        _hasResolvableMessages = false;

        foreach (var message in Messages
                     .Where(m => m is ChatMessageErrorViewModel or ChatMessageWithButtonViewModel)
                     .ToList())
            Messages.Remove(message);
        _notConnectedMessage = null;
    }

    private ChatMessageReasoningViewModel GetOrCreateAssistantReasoningMessage(string? reasoningId,
        string? agentId = null)
    {
        if (!string.IsNullOrWhiteSpace(reasoningId))
        {
            if (_assistantReasoningById.TryGetValue(reasoningId, out var existing))
                return existing;

            var created = new ChatMessageReasoningViewModel(reasoningId);
            AddMessage(created, agentId);
            _assistantReasoningById[reasoningId] = created;
            return created;
        }

        var activeReasoning = new ChatMessageReasoningViewModel(reasoningId);
        AddMessage(activeReasoning, agentId);

        return activeReasoning;
    }

    private ChatMessageAssistantViewModel GetOrCreateAssistantMessage(string? messageId, string? agentId = null)
    {
        if (Messages.LastOrDefault() is ChatMessageAssistantViewModel { MessageId: "init" } initMessage)
        {
            Messages.Remove(initMessage);
            _turnAssistantMessages.Remove(initMessage);
        }

        if (!string.IsNullOrWhiteSpace(messageId))
        {
            if (_assistantMessagesById.TryGetValue(messageId, out var existing))
                return existing;

            var created = new ChatMessageAssistantViewModel(messageId);
            AddMessage(created, agentId);
            _assistantMessagesById[messageId] = created;
            if (agentId == null) _turnAssistantMessages.Add(created);
            return created;
        }

        var activeAssistantMessage = new ChatMessageAssistantViewModel(messageId);
        AddMessage(activeAssistantMessage, agentId);
        if (agentId == null) _turnAssistantMessages.Add(activeAssistantMessage);

        return activeAssistantMessage;
    }

    /// <param name="model">
    /// Model that answered the turn, when the chat service reports it. Used for messages that did
    /// not carry the information themselves.
    /// </param>
    private void FinishTurn(string? model = null)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ShowModelOfLastAnswer(model);

            foreach (var message in EnumerateAllMessages())
            {
                switch (message)
                {
                    case ChatMessageAssistantViewModel assistant:
                        assistant.IsStreaming = false;
                        break;
                    case ChatMessageReasoningViewModel reasoning:
                        reasoning.IsStreaming = false;
                        break;
                    // A background sub-agent outlives the turn that started it, so it keeps
                    // running (and receiving events) until its own completion event arrives.
                    case ChatMessageSubAgentViewModel { IsRunning: true, IsBackground: false } subAgent:
                        // The turn ended without a completion event (e.g. after an abort).
                        FinishRunningItems(subAgent);
                        subAgent.IsRunning = false;
                        subAgent.IsExpanded = false;
                        subAgent.StatusText = "Stopped";
                        break;
                }
            }

            foreach (var id in _subAgents.Where(x => !x.Value.IsRunning).Select(x => x.Key).ToArray())
                _subAgents.Remove(id);

            foreach (var claim in _pendingSubAgentTools.Where(x => !x.Value.IsRunning).Select(x => x.Key).ToArray())
                _pendingSubAgentTools.Remove(claim);

            OfferPlanOptionsIfMissing();

            IsBusy = false;
            // Safety: never let a status (e.g. steering) stick past the end of a turn.
            ResetActivity();

            // The turn is complete — persist it immediately instead of waiting for the
            // throttled auto save.
            _host.SaveNow();
        });
    }

    /// <summary>
    /// Attributes the answer to the model that produced it, on the last message of the finished turn
    /// only. Earlier turns keep their own attribution, and messages inside a sub-agent block are left
    /// alone because that block names its own model.
    /// </summary>
    private void ShowModelOfLastAnswer(string? model)
    {
        var turnMessages = _turnAssistantMessages;
        _turnAssistantMessages = [];

        var last = turnMessages.LastOrDefault();
        if (last == null) return;

        if (string.IsNullOrWhiteSpace(last.Model)) last.Model = model;
        if (string.IsNullOrWhiteSpace(last.Model)) return;

        // Intermediate messages of the same turn stay unlabelled, so the turn ends with a single
        // "answered by" line.
        foreach (var message in turnMessages)
            message.ShowModel = ReferenceEquals(message, last);
    }

    /// <summary>
    /// Ends a planning turn with the same choice the agent would offer: start the implementation or
    /// keep planning. Chat backends that report a finished plan themselves already added the block;
    /// this is the fallback for a planning turn that simply ended with the plan written out.
    /// </summary>
    private void OfferPlanOptionsIfMissing()
    {
        if (_planOptionsOffered) return;
        if (AgentService.SelectedAgent is not { TurnMode: ChatAgentTurnMode.Plan }) return;

        // Only after the agent actually said something — an aborted or empty turn has no plan.
        if (Messages.LastOrDefault() is not ChatMessageAssistantViewModel { Content.Length: > 0 }) return;

        _planOptionsOffered = true;

        var start = new RelayCommand<Control?>(sender =>
        {
            AgentService.SelectAgent(BuiltInChatAgents.Agent);
            CurrentMessage = "Implement the plan.";
            _ = SendInternalAsync(ChatSendMode.Send);
        });

        var update = new RelayCommand<Control?>(_ => { });

        AddMessage(new ChatMessagePlanReadyViewModel(
            new ChatPlanReadyEvent("The plan is ready. Start the implementation or have it changed.",
                null, start, update)));
        NotifyContentAdded();
    }

    /// <summary>All messages of the conversation, including those nested in sub-agent blocks.</summary>
    private IEnumerable<IChatMessage> EnumerateAllMessages()
    {
        return EnumerateMessages(Messages);
    }

    private static IEnumerable<IChatMessage> EnumerateMessages(IEnumerable<IChatMessage> messages)
    {
        foreach (var message in messages.ToArray())
        {
            yield return message;

            if (message is not ChatMessageSubAgentViewModel subAgent) continue;

            foreach (var nested in EnumerateMessages(subAgent.Items))
                yield return nested;
        }
    }

    private void OnEventReceived(object? sender, ChatEvent e)
    {
        switch (e)
        {
            case ChatMessageDeltaEvent x:
            {
                if (string.IsNullOrWhiteSpace(x.Content)) break;
                Dispatcher.UIThread.Post(() =>
                {
                    if (x.AgentId == null) DismissResolvedMessages();
                    var message = GetOrCreateAssistantMessage(x.MessageId, x.AgentId);
                    message.IsStreaming = true;
                    message.Content += x.Content;
                    SetSubAgentStatus(x.AgentId, "Responding…");
                    SetPhase(x.AgentId, ChatActivityPhase.Writing);
                    NotifyContentAdded();
                });
                break;
            }
            case ChatMessageEvent x:
            {
                if (string.IsNullOrWhiteSpace(x.Content)) break;
                Dispatcher.UIThread.Post(() =>
                {
                    if (x.AgentId == null) DismissResolvedMessages();
                    var message = GetOrCreateAssistantMessage(x.MessageId, x.AgentId);
                    message.Content = x.Content;
                    message.IsStreaming = false;
                    if (!string.IsNullOrWhiteSpace(x.Model)) message.Model = x.Model;
                    SetPhase(x.AgentId, ChatActivityPhase.Working);
                    NotifyContentAdded();
                });
                break;
            }
            case ChatReasoningDeltaEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var message = GetOrCreateAssistantReasoningMessage(x.ReasoningId, x.AgentId);
                    message.IsStreaming = true;
                    message.Content += x.Content;
                    SetSubAgentStatus(x.AgentId, "Thinking…");
                    SetPhase(x.AgentId, ChatActivityPhase.Thinking);
                    NotifyContentAdded();
                });
                break;
            }
            case ChatReasoningEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var message = GetOrCreateAssistantReasoningMessage(x.ReasoningId, x.AgentId);
                    message.Content = x.Content;
                    message.IsStreaming = false;
                    SetPhase(x.AgentId, ChatActivityPhase.Working);
                    NotifyContentAdded();
                });
                break;
            }
            case ChatSubAgentStartedEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    AddSubAgent(x);
                    NotifyContentAdded();
                });
                break;
            }
            case ChatSubAgentCompletedEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    CompleteSubAgent(x);
                    NotifyContentAdded();
                });
                break;
            }
            case ChatToolExecutionStartEvent x:
            {
                // Tool calls of the main agent are rendered from the function provider events,
                // which also carry the live output and a stop button. Tools of the backend itself
                // are only reflected in the status.
                if (x.AgentId == null)
                {
                    if (!x.IsClientTool && !string.IsNullOrWhiteSpace(x.ToolCallId))
                        Dispatcher.UIThread.Post(() =>
                        {
                            _activity.ToolStarted(x.ToolCallId, x.Tool);
                            UpdateWorkingStatus();
                        });
                    break;
                }

                Dispatcher.UIThread.Post(() =>
                {
                    StartSubAgentTool(x);
                    NotifyContentAdded();
                });
                break;
            }
            case ChatToolExecutionCompleteEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    CompleteSubAgentTool(x);
                    _activity.ToolCompleted(x.ToolCallId);
                    UpdateWorkingStatus();
                });
                break;
            }
            case ChatSkillLoadedEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    AddMessage(new ChatMessageSkillViewModel(x.SkillName, x.Content), x.AgentId);
                    NotifyContentAdded();
                });
                break;
            }
            case ChatUserMessageEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (TryDequeuePendingLocal(x.Content, out var pending))
                    {
                        switch (pending.Mode)
                        {
                            case ChatSendMode.Queue when pending.QueuedView != null:
                                // The agent activated the queued message — promote it into the flow.
                                QueuedMessages.Remove(pending.QueuedView);
                                AddMessage(pending.QueuedView);
                                IsBusy = true;
                                NotifyContentAdded();
                                break;
                            case ChatSendMode.Steer:
                                // Steering has been applied to the current turn.
                                _activity.IsSteering = false;
                                UpdateWorkingStatus();
                                break;
                            // Normal send: already visible, nothing to do.
                        }

                        return;
                    }

                    if (TryDiscardCancelledQueuedMessage(x.Content)) return;

                    // Originates from a remote session user; show it and mark busy.
                    AddMessage(new ChatMessageUserViewModel(x.Content));
                    IsBusy = true;
                    NotifyContentAdded();
                });
                break;
            }
            case ChatButtonEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    // Replace (don't stack) a prompt that is repeated, e.g. on every send while the plan lacks OneWare Agents.
                    foreach (var existing in Messages.OfType<ChatMessageWithButtonViewModel>()
                                 .Where(m => m.Event.Message == x.Message && m.Event.ButtonText == x.ButtonText)
                                 .ToList())
                        Messages.Remove(existing);
                    AddMessage(new ChatMessageWithButtonViewModel(x));
                    _hasResolvableMessages = true;
                    NotifyContentAdded();
                });
                break;
            }
            case ChatPermissionRequestEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var msg = new ChatMessagePermissionRequestViewModel(x);
                    msg.CloseAction = () =>
                    {
                        Messages.Remove(msg);
                        _activity.PromptClosed(msg);
                        UpdateWorkingStatus();
                    };
                    AddMessage(msg);
                    _activity.PromptOpened(msg, "Waiting for approval…");
                    MarkAttentionIfHidden();
                    UpdateWorkingStatus();
                    NotifyContentAdded();
                });
                break;
            }
            case ChatPlanReadyEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _planOptionsOffered = true;
                    AddMessage(new ChatMessagePlanReadyViewModel(x));
                    NotifyContentAdded();
                });
                break;
            }
            case ChatUserInputRequestEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var msg = new ChatMessageUserInputRequestViewModel(x);
                    AddMessage(msg);
                    _activity.PromptOpened(msg, "Waiting for your answer…");
                    MarkAttentionIfHidden();
                    msg.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName != nameof(ChatMessageUserInputRequestViewModel.IsAnswered)) return;
                        _activity.PromptClosed(msg);
                        UpdateWorkingStatus();
                    };
                    UpdateWorkingStatus();
                    NotifyContentAdded();
                });
                break;
            }
            case ChatErrorEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    AddErrorMessage(x.Message, x.AgentId);
                    NotifyContentAdded();
                });
                break;
            }
            case ChatIdleEvent x:
            {
                FinishTurn(x.Model);
                break;
            }
            case ChatClearMessagesEvent:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Messages.Clear();
                    QueuedMessages.Clear();
                    _pendingLocalMessages.Clear();
                    _cancelledQueuedMessages.Clear();
                    _assistantMessagesById.Clear();
                    _turnAssistantMessages.Clear();
                    _assistantReasoningById.Clear();
                    _subAgents.Clear();
                    _pendingSubAgentTools.Clear();
                    _notConnectedMessage = null;
                    if (Service != null)
                        _host.OnSessionChanged(this);
                });
                break;
            }
        }
    }

    private void OnStatusChanged(object? sender, StatusEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsConnected = e.IsConnected;
            StatusText = e.StatusText;
        });
    }

    private void OnSessionReset(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Keep the visible transcript: a session reset (e.g. after a CLI update or
            // re-login) starts a fresh backend session, but wiping the conversation from
            // the view is unnecessary. Only in-flight state tied to the old session is
            // dropped, and any still-streaming messages are finalized.
            foreach (var message in Messages.OfType<ChatMessageAssistantViewModel>())
                message.IsStreaming = false;
            foreach (var message in Messages.OfType<ChatMessageReasoningViewModel>())
                message.IsStreaming = false;

            // Sweep transient service-status prompts (e.g. "Update Copilot CLI",
            // "Login with GitHub") — after a reset they have been acted on.
            foreach (var button in Messages.OfType<ChatMessageWithButtonViewModel>().ToList())
                Messages.Remove(button);
            if (_notConnectedMessage != null)
            {
                Messages.Remove(_notConnectedMessage);
                _notConnectedMessage = null;
            }

            QueuedMessages.Clear();
            _pendingLocalMessages.Clear();
            _cancelledQueuedMessages.Clear();
            _assistantMessagesById.Clear();
            _turnAssistantMessages.Clear();
            _assistantReasoningById.Clear();
            IsBusy = false;
            ResetActivity();

            // If the backend lost its session (e.g. the CLI was reinstalled while it
            // was never connected), ask it to resume the conversation of this chat
            // instead of silently starting a fresh one.
            if (Service is IChatServiceWithSessions sessions &&
                string.IsNullOrWhiteSpace(sessions.CurrentSessionId) &&
                !string.IsNullOrWhiteSpace(RememberedSessionId))
            {
                _ = RestoreRememberedSessionAsync(sessions, RememberedSessionId);
            }
            else
            {
                _host.OnSessionChanged(this);
            }
        });
    }

    private async Task RestoreRememberedSessionAsync(IChatServiceWithSessions sessions, string sessionId)
    {
        try
        {
            await sessions.LoadSessionAsync(sessionId);
        }
        catch (Exception)
        {
            // Best effort — the service keeps the requested id and resumes lazily.
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!string.Equals(sessions.CurrentSessionId, sessionId, StringComparison.Ordinal))
                ShowReplacementSessionIfNeeded(sessionId);
            else
                _host.OnSessionChanged(this);
        });
    }

    internal void HandleFunctionStarted(AiFunctionStartedEvent function)
    {
        var newMessage = new ChatMessageToolViewModel(function.Id, function.FunctionName)
        {
            IsToolRunning = true,
            ToolOutput = $"{function.Detail}",
            SourceToolCallId = function.ToolCallId
        };
        newMessage.StopCommand = new RelayCommand(
            () => _aiFunctionProvider.CancelFunction(newMessage.Id),
            () => newMessage.IsToolRunning);

        _activity.ToolStarted(function.Id, function.FunctionName);
        UpdateWorkingStatus();

        var subAgent = DequeueSubAgentToolClaim(function.ToolCallId);
        if (subAgent != null)
        {
            subAgent.Items.Add(newMessage);
            subAgent.StatusText = function.FunctionName;
        }
        else
        {
            AddMessage(newMessage);
        }

        NotifyContentAdded();
    }

    internal void HandleFunctionCompleted(AiFunctionCompletedEvent function)
    {
        _activity.ToolCompleted(function.Id);
        UpdateWorkingStatus();

        var toolFinished = FindToolMessage(function.Id);
        if (toolFinished == null) return;

        toolFinished.IsToolRunning = false;
        toolFinished.StopCommand = null;
        toolFinished.IsSuccessful = function.Result;
        if (!string.IsNullOrWhiteSpace(function.ToolOutput))
        {
            if (string.IsNullOrWhiteSpace(toolFinished.ToolOutput))
                toolFinished.ToolOutput += '\n';
            toolFinished.ToolOutput += function.ToolOutput;
        }

        RequestSaveState();
    }

    internal void HandleFunctionProgress(AiFunctionProgressEvent progress)
    {
        var tool = FindToolMessage(progress.Id);
        if (tool == null || !tool.IsToolRunning) return;

        tool.ToolOutput = progress.Output;
        RequestSaveState();
    }

    // ── Sub-agents ────────────────────────────────────────────────────────────

    private void AddSubAgent(ChatSubAgentStartedEvent started)
    {
        var subAgent = new ChatMessageSubAgentViewModel(started.Id, started.DisplayName)
        {
            Description = started.Description,
            Instructions = started.Instructions,
            Model = started.Model,
            IsBackground = started.IsBackground,
            StatusText = started.IsBackground ? "Running in background…" : "Working…"
        };

        // A nested sub-agent belongs into the block of the agent that spawned it.
        AddMessage(subAgent, started.ParentSubAgentId);
        _subAgents[started.Id] = subAgent;

        // A background sub-agent outlives the turn, so the turn does not wait for it.
        if (!started.IsBackground)
        {
            _activity.SubAgentStarted(started.Id, started.DisplayName);
            UpdateWorkingStatus();
        }
    }

    private void CompleteSubAgent(ChatSubAgentCompletedEvent completed)
    {
        _activity.SubAgentCompleted(completed.Id);
        UpdateWorkingStatus();

        if (!_subAgents.Remove(completed.Id, out var subAgent)) return;

        FinishRunningItems(subAgent);
        subAgent.Complete(completed);
    }

    /// <summary>Stops spinners of nested entries that never reported a result of their own.</summary>
    private static void FinishRunningItems(ChatMessageSubAgentViewModel subAgent)
    {
        foreach (var item in subAgent.Items.ToArray())
        {
            switch (item)
            {
                case ChatMessageToolViewModel { IsToolRunning: true } tool:
                    tool.IsToolRunning = false;
                    tool.StopCommand = null;
                    break;
                case ChatMessageAssistantViewModel assistant:
                    assistant.IsStreaming = false;
                    break;
                case ChatMessageReasoningViewModel reasoning:
                    reasoning.IsStreaming = false;
                    break;
            }
        }
    }

    /// <summary>Only the main agent's phase is shown; sub-agents report their phase in their own block.</summary>
    private void SetPhase(string? agentId, ChatActivityPhase phase)
    {
        if (agentId != null || _activity.Phase == phase) return;

        _activity.Phase = phase;
        UpdateWorkingStatus();
    }

    private void OnBusyChanged(bool busy)
    {
        _activity.Reset();
        UpdateWorkingStatus();
        if (!busy) MarkAttentionIfHidden();
    }

    private void ResetActivity()
    {
        _activity.Reset();
        UpdateWorkingStatus();
    }

    private void UpdateWorkingStatus()
    {
        WorkingStatusText = IsWaitingForConnection && !IsBusy
            ? StatusText
            : _activity.GetText();
    }

    private void SetSubAgentStatus(string? agentId, string status)
    {
        if (agentId == null) return;
        if (!_subAgents.TryGetValue(agentId, out var subAgent)) return;

        subAgent.StatusText = status;
    }

    /// <summary>
    /// A tool call a sub-agent started. Tools OneWare executes itself are only announced here and
    /// rendered once the function provider reports them, so they keep their live output.
    /// </summary>
    private void StartSubAgentTool(ChatToolExecutionStartEvent start)
    {
        if (start.AgentId == null || !_subAgents.TryGetValue(start.AgentId, out var subAgent)) return;

        subAgent.StatusText = start.Tool;

        if (string.IsNullOrWhiteSpace(start.ToolCallId)) return;

        if (start.IsClientTool)
        {
            // The tool call can already be shown in the main flow when the function provider
            // reported it before this event arrived.
            if (!TryAdoptRunningTool(subAgent, start.ToolCallId))
                _pendingSubAgentTools[start.ToolCallId] = subAgent;

            return;
        }

        subAgent.Items.Add(new ChatMessageToolViewModel(start.ToolCallId, start.Tool)
        {
            IsToolRunning = true,
            ToolOutput = start.Detail,
            SourceToolCallId = start.ToolCallId
        });
    }

    private void CompleteSubAgentTool(ChatToolExecutionCompleteEvent complete)
    {
        var tool = FindToolMessage(complete.ToolCallId);
        if (tool == null || !tool.IsToolRunning) return;

        tool.IsToolRunning = false;
        tool.StopCommand = null;
        tool.IsSuccessful = complete.Success;

        if (!string.IsNullOrWhiteSpace(complete.Output))
        {
            tool.ToolOutput = string.IsNullOrWhiteSpace(tool.ToolOutput)
                ? complete.Output
                : tool.ToolOutput + "\n" + complete.Output;
        }

        RequestSaveState();
    }

    /// <summary>
    /// Moves a tool that was already shown in the main flow into a sub-agent block. Needed because
    /// the function provider can report a tool call before the chat service tells which sub-agent
    /// it belongs to.
    /// </summary>
    private bool TryAdoptRunningTool(ChatMessageSubAgentViewModel subAgent, string toolCallId)
    {
        var running = Messages.OfType<ChatMessageToolViewModel>().LastOrDefault(x =>
            string.Equals(x.SourceToolCallId, toolCallId, StringComparison.Ordinal));

        if (running == null) return false;

        Messages.Remove(running);
        subAgent.Items.Add(running);
        subAgent.StatusText = running.ToolName;
        return true;
    }

    private ChatMessageSubAgentViewModel? DequeueSubAgentToolClaim(string? toolCallId)
    {
        if (string.IsNullOrWhiteSpace(toolCallId)) return null;
        if (!_pendingSubAgentTools.Remove(toolCallId, out var subAgent)) return null;

        return subAgent;
    }

    internal ChatMessageToolViewModel? FindToolMessage(string id)
    {
        return EnumerateAllMessages().OfType<ChatMessageToolViewModel>()
            .LastOrDefault(x => string.Equals(x.Id, id, StringComparison.Ordinal));
    }

    /// <summary>
    /// Raises <see cref="ContentAdded"/> and schedules a state save, so a crash cannot take the
    /// whole conversation with it.
    /// </summary>
    private void NotifyContentAdded()
    {
        ContentAdded?.Invoke(this, EventArgs.Empty);
        RequestSaveState();
    }

    /// <summary>
    /// Schedules a throttled state save, so a crash cannot take the whole conversation with it.
    /// </summary>
    private void RequestSaveState()
    {
        IsDirty = true;
        _host.RequestSave(this);
    }

    /// <summary>Messages of the conversation in their stored form.</summary>
    internal List<ChatMessageState> BuildMessageStates() => ChatTranscript.BuildStates(Messages);

    internal void LoadMessagesFromStates(IReadOnlyCollection<ChatMessageState> states)
    {
        Messages.Clear();
        _assistantMessagesById.Clear();
        _turnAssistantMessages.Clear();
        _assistantReasoningById.Clear();
        _subAgents.Clear();
        _pendingSubAgentTools.Clear();

        foreach (var messageState in states)
        {
            if (ChatTranscript.TryCreateMessage(messageState, out var message))
                Messages.Add(message);
        }
    }

    /// <summary>Drops the visible conversation, e.g. because the stored history of the service was discarded.</summary>
    internal void ClearTranscript()
    {
        Messages.Clear();
        _notConnectedMessage = null;
        _assistantMessagesById.Clear();
        _turnAssistantMessages.Clear();
        _assistantReasoningById.Clear();
        _subAgents.Clear();
        _pendingSubAgentTools.Clear();
        QueuedMessages.Clear();
        _pendingLocalMessages.Clear();
        _cancelledQueuedMessages.Clear();
        RememberedSessionId = null;
        Title = DefaultTitle;
        IsDirty = false;
    }

    /// <summary>Stops listening to the service when the chat is unloaded.</summary>
    internal void Detach()
    {
        Service.EventReceived -= OnEventReceived;
        Service.StatusChanged -= OnStatusChanged;
        Service.SessionReset -= OnSessionReset;
        Service.PropertyChanged -= OnChatServicePropertyChanged;
        ReleaseConnectionWaiters(false);
    }
}
