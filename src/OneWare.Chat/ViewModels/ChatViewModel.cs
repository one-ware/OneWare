using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using OneWare.Chat.Services;
using OneWare.Chat.ViewModels.ChatMessages;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Chat.ViewModels;

public partial class ChatViewModel : ExtendedTool, IChatManagerService
{
    public const string IconKey = "Bootstrap.ChatLeft";

    /// <summary>
    /// Settings key for the maximum number of stored chat sessions per chat service.
    /// </summary>
    public const string MaxSessionHistoryKey = "Chat_MaxSessionHistory";

    public const int DefaultMaxSessionHistory = 50;

    private readonly IMainDockService _mainDockService;
    private readonly IAiFunctionProvider _aiFunctionProvider;
    private readonly ISettingsService _settingsService;
    private ChatMessageErrorViewModel? _notConnectedMessage;

    // Set while the conversation shows errors or service prompts that a successful answer resolves.
    private bool _hasResolvableMessages;
    private readonly string _statePath;
    private readonly string _historyRootPath;

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
    
    private readonly Dictionary<string, string> _selectedSessionByService = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ChatSessionHistoryItem>> _historyByService = new(StringComparer.Ordinal);

    private bool _initialized;

    // Single-flight guard: initialization is kicked off automatically when a service is
    // selected. A send that happens while that is still running must join the running
    // operation instead of starting a second (destructive) initialization.
    private Task<bool>? _initializeTask;
    private IChatService? _initializeTaskService;

    // Completion sources waiting for the selected service to report a connected status.
    private readonly List<TaskCompletionSource<bool>> _connectionWaiters = [];

    private static readonly TimeSpan ConnectionWaitTimeout = TimeSpan.FromMinutes(2);
    // FIFO of messages sent locally so the echoed ChatUserMessageEvent can be matched
    // (suppressed for normal/steered sends, or used to activate a queued message).
    private readonly Queue<PendingLocalMessage> _pendingLocalMessages = new();
    private readonly List<CancelledQueuedMessage> _cancelledQueuedMessages = [];

    private const string DefaultWorkingStatus = "Working…";

    private sealed record PendingLocalMessage(
        string Content,
        ChatSendMode Mode,
        ChatMessageUserViewModel? QueuedView);

    private sealed record CancelledQueuedMessage(string Content, DateTimeOffset ExpiresAt);

    private static readonly JsonSerializerOptions ChatStateSerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Upper bound for how much of a conversation can be lost when the IDE crashes: the
    // transcript is persisted at most this long after the first pending change.
    private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromSeconds(5);

    private DispatcherTimer? _autoSaveTimer;

    public ChatViewModel(IAiFunctionProvider aiFunctionProvider, IMainDockService mainDockService,
        AiFileEditService aiFileEditService, IPaths paths, ISettingsService settingsService,
        IApplicationStateService applicationStateService, IChatAgentService chatAgentService) : base(IconKey)
    {
        AgentService = chatAgentService;
        Id = "AI_Chat";
        Title = "AI Chat";

        aiFunctionProvider.FunctionStarted += OnFunctionStarted;
        aiFunctionProvider.FunctionCompleted += OnFunctionCompleted;
        aiFunctionProvider.FunctionProgress += OnFunctionProgress;

        _aiFunctionProvider = aiFunctionProvider;
        _mainDockService = mainDockService;
        _settingsService = settingsService;

        var chatDirectory = Path.Combine(paths.AppDataDirectory, "Chat");
        _statePath = Path.Combine(chatDirectory, "ChatState.json");
        _historyRootPath = Path.Combine(chatDirectory, "History");

        AiFileEditService = aiFileEditService;

        NewChatCommand = new AsyncRelayCommand(NewChatAsync);
        SendCommand = new AsyncRelayCommand(() => SendInternalAsync(ChatSendMode.Send), CanSend);
        SteerCommand = new AsyncRelayCommand(() => SendInternalAsync(ChatSendMode.Steer), CanSteerOrQueue);
        QueueCommand = new AsyncRelayCommand(() => SendInternalAsync(ChatSendMode.Queue), CanSteerOrQueue);
        AbortCommand = new AsyncRelayCommand(AbortAsync, CanAbort);
        RemoveQueuedMessageCommand =
            new AsyncRelayCommand<ChatMessageUserViewModel>(RemoveQueuedMessageAsync, CanRemoveQueuedMessage);
        InitializeCurrentCommand = new AsyncRelayCommand(InitializeCurrentAsync);

        QueuedMessages.CollectionChanged += (_, _) => RemoveQueuedMessageCommand.NotifyCanExecuteChanged();
        Messages.CollectionChanged += (_, _) => RequestSaveState();
        applicationStateService.RegisterShutdownAction(SaveState);
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
            }
        }
    }

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
            }
        }
    }

    public string StatusText
    {
        get;
        set => SetProperty(ref field, value);
    } = "Starting...";

    public ObservableCollection<IChatMessage> Messages { get; set; } = new();

    /// <summary>
    /// Messages the user has queued while the agent is busy. Rendered (dimmed) below the
    /// conversation until the agent activates them.
    /// </summary>
    public ObservableCollection<IChatMessage> QueuedMessages { get; } = new();

    /// <summary>
    /// Text shown next to the working spinner. Switches to "Steering…" while a steered message
    /// is being applied to the current turn.
    /// </summary>
    public string WorkingStatusText
    {
        get;
        set => SetProperty(ref field, value);
    } = DefaultWorkingStatus;

    /// <summary>Whether the current planning turn already offered how to continue.</summary>
    private bool _planOptionsOffered;

    public ObservableCollection<IChatService> ChatServices { get; } = [];

    /// <summary>
    /// Selectable chat agents ("Agent", "Plan", "Ask" and custom ones), bound by the agent selector.
    /// </summary>
    public IChatAgentService AgentService { get; }

    public ObservableCollection<ChatSessionHistoryItem> SessionHistory { get; } = [];

    public ChatSessionHistoryItem? SelectedSessionHistory
    {
        get;
        set
        {
            if (SetProperty(ref field, value) && value != null)
            {
                _ = LoadSessionAsync(value);
            }
        }
    }

    public IChatService? SelectedChatService
    {
        get;
        set
        {
            var oldValue = field;
            if (!SetProperty(ref field, value)) return;

            if (oldValue != null)
            {
                StoreCurrentMessages(oldValue.Name, oldValue);
                oldValue.EventReceived -= OnEventReceived;
                oldValue.StatusChanged -= OnStatusChanged;
                oldValue.SessionReset -= OnSessionReset;
                oldValue.PropertyChanged -= OnChatServicePropertyChanged;
                ReleaseConnectionWaiters(false);
            }

            Blocker = value?.Blocker;

            if (value == null) return;

            value.EventReceived += OnEventReceived;
            value.StatusChanged += OnStatusChanged;
            value.SessionReset += OnSessionReset;
            value.PropertyChanged += OnChatServicePropertyChanged;
            
            _ = InitializeAndRestoreCurrentServiceAsync(value);
        }
    }

    /// <summary>
    /// Set while the selected service cannot be used (e.g. login or subscription required). The chat is hidden
    /// and replaced by a panel describing the blocker until the service clears it.
    /// </summary>
    public ChatServiceBlocker? Blocker
    {
        get;
        private set
        {
            if (SetProperty(ref field, value)) OnPropertyChanged(nameof(IsBlocked));
        }
    }

    public bool IsBlocked => Blocker != null;

    private void OnChatServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IChatService.Blocker)) return;

        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(sender, SelectedChatService))
                Blocker = SelectedChatService?.Blocker;
        });
    }

    public AiFileEditService AiFileEditService { get; }

    public RelayCommand<AiEditViewModel> ShowEditCommand => new(ShowEdit);

    public AsyncRelayCommand NewChatCommand { get; }

    public AsyncRelayCommand SendCommand { get; }

    public AsyncRelayCommand SteerCommand { get; }

    public AsyncRelayCommand QueueCommand { get; }

    public AsyncRelayCommand AbortCommand { get; }

    public AsyncRelayCommand<ChatMessageUserViewModel> RemoveQueuedMessageCommand { get; }

    public AsyncRelayCommand InitializeCurrentCommand { get; }

    public override void InitializeContent()
    {
        if (_initialized) return;
        _initialized = true;

        LoadState();
    }

    private async Task<bool> InitializeCurrentAsync()
    {
        var service = SelectedChatService;
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

        if (SelectedChatService != service) return status;

        IsInitialized = status;

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

    private async Task InitializeAndRestoreCurrentServiceAsync(IChatService chatService)
    {
        LoadMessagesForService(chatService.Name);
        
        var initialized = await InitializeCurrentAsync();
        if (!initialized || SelectedChatService != chatService) return;

        if (chatService is not IChatServiceWithSessions serviceWithSessions) return;

        var targetSessionId = _selectedSessionByService.TryGetValue(chatService.Name, out var sessionId)
            ? sessionId
            : SessionHistory.FirstOrDefault()?.SessionId;

        if (string.IsNullOrWhiteSpace(targetSessionId)) return;

        await serviceWithSessions.LoadSessionAsync(targetSessionId);
    }

    private async Task NewChatAsync()
    {
        // Agent files may have been added or edited since the last chat started.
        AgentService.Refresh();

        if (SelectedChatService != null)
        {
            StoreCurrentMessages(SelectedChatService.Name, SelectedChatService);

            if (!IsInitialized)
            {
                await InitializeCurrentAsync();
            }

            await AbortAsync();
            await SelectedChatService.NewChatAsync();

            UpdateSelectedSessionFromService(SelectedChatService);
        }

        Messages.Clear();
        QueuedMessages.Clear();
        _pendingLocalMessages.Clear();
        _cancelledQueuedMessages.Clear();
        WorkingStatusText = DefaultWorkingStatus;
        _assistantMessagesById.Clear();
        _turnAssistantMessages.Clear();
        _assistantReasoningById.Clear();
        _notConnectedMessage = null;
    }

    private async Task SendInternalAsync(ChatSendMode mode)
    {
        var prompt = CurrentMessage.Trim();
        if (string.IsNullOrWhiteSpace(prompt)) return;

        // The next planning turn gets its own decision block. Steering or queueing happens inside a
        // running turn and must not bring the offer of the current one back.
        if (mode == ChatSendMode.Send) _planOptionsOffered = false;

        var chatService = SelectedChatService;
        if (chatService == null)
        {
            AddErrorMessage("No chat service selected.");
            return;
        }

        var initialized = IsInitialized;
        if (!initialized)
        {
            initialized = await InitializeCurrentAsync();
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
        if (!ReferenceEquals(SelectedChatService, chatService)) return;

        if (_notConnectedMessage != null)
        {
            Messages.Remove(_notConnectedMessage);
            _notConnectedMessage = null;
        }

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
                WorkingStatusText = "Steering…";
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
            if (mode == ChatSendMode.Steer) WorkingStatusText = DefaultWorkingStatus;
        }
    }

    private bool CanSteerOrQueue() =>
        IsConnected && IsBusy && !string.IsNullOrWhiteSpace(CurrentMessage);

    private async Task AbortAsync()
    {
        if (SelectedChatService == null) return;

        var queueCleared = QueuedMessages.Count == 0;
        if (!queueCleared)
        {
            try
            {
                queueCleared = await SelectedChatService.ClearQueuedMessagesAsync();
            }
            catch (Exception ex)
            {
                AddErrorMessage($"Failed to clear queued messages: {ex.Message}");
            }
        }

        try
        {
            await SelectedChatService.AbortAsync();
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
        if (message == null || SelectedChatService == null || !CanRemoveQueuedMessage(message)) return;

        bool removed;
        try
        {
            removed = await SelectedChatService.RemoveMostRecentQueuedMessageAsync();
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
            // Safety: never let the steering indicator stick past the end of a turn.
            WorkingStatusText = DefaultWorkingStatus;

            // The turn is complete — persist it immediately instead of waiting for the
            // throttled auto save.
            SaveState();
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
                // which also carry the live output and a stop button.
                if (x.AgentId == null) break;

                Dispatcher.UIThread.Post(() =>
                {
                    StartSubAgentTool(x);
                    NotifyContentAdded();
                });
                break;
            }
            case ChatToolExecutionCompleteEvent x:
            {
                Dispatcher.UIThread.Post(() => CompleteSubAgentTool(x));
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
                                WorkingStatusText = DefaultWorkingStatus;
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
                    // Replace (don't stack) a prompt that is repeated, e.g. on every send while the plan lacks Cloud AI.
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
                    msg.CloseAction = () => Messages.Remove(msg);
                    AddMessage(msg);
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
                    AddMessage(new ChatMessageUserInputRequestViewModel(x));
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
                    if (SelectedChatService != null)
                        UpdateSelectedSessionFromService(SelectedChatService);
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
            WorkingStatusText = DefaultWorkingStatus;

            if (SelectedChatService is { } service)
            {
                // If the backend lost its session (e.g. the CLI was reinstalled while it
                // was never connected), ask it to resume the conversation we remember for
                // this service instead of silently starting a fresh one.
                if (service is IChatServiceWithSessions sessions &&
                    string.IsNullOrWhiteSpace(sessions.CurrentSessionId) &&
                    _selectedSessionByService.TryGetValue(service.Name, out var remembered) &&
                    !string.IsNullOrWhiteSpace(remembered))
                {
                    _ = RestoreRememberedSessionAsync(service, sessions, remembered);
                }
                else
                {
                    UpdateSelectedSessionFromService(service);
                }
            }
        });
    }

    private async Task RestoreRememberedSessionAsync(IChatService service, IChatServiceWithSessions sessions,
        string sessionId)
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
            if (SelectedChatService == service)
                UpdateSelectedSessionFromService(service);
        });
    }

    public void RegisterChatService(IChatService chatService)
    {
        ChatServices.Add(chatService);

        if (chatService is IChatServiceWithHistoryReset historyReset)
            historyReset.HistoryCleared += OnServiceHistoryCleared;
    }

    private void OnServiceHistoryCleared(object? sender, EventArgs e)
    {
        if (sender is not IChatService service) return;

        if (Dispatcher.UIThread.CheckAccess())
            ClearServiceHistory(service);
        else
            Dispatcher.UIThread.Post(() => ClearServiceHistory(service));
    }

    /// <summary>
    /// Discards every stored chat of a service, e.g. because a different account logged in.
    /// </summary>
    private void ClearServiceHistory(IChatService service)
    {
        var serviceName = service.Name;

        _selectedSessionByService.Remove(serviceName);
        _historyByService.Remove(serviceName);

        var serviceDirectory = GetServiceHistoryDirectory(serviceName);
        try
        {
            if (Directory.Exists(serviceDirectory))
                Directory.Delete(serviceDirectory, true);
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<Microsoft.Extensions.Logging.ILogger>()
                ?.Warning($"Deleting chat history of '{serviceName}' failed", ex);
        }

        if (SelectedChatService == service)
        {
            // Drop the visible transcript too, otherwise it would be saved into the new account's session.
            SessionHistory.Clear();
            SelectedSessionHistory = null;
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
        }

        if (_initialized) SaveState();
    }

    private void OnFunctionStarted(object? sender, AiFunctionStartedEvent function)
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

    private void OnFunctionCompleted(object? sender, AiFunctionCompletedEvent function)
    {
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

    private void OnFunctionProgress(object? sender, AiFunctionProgressEvent progress)
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
            Model = started.Model,
            IsBackground = started.IsBackground,
            StatusText = started.IsBackground ? "Running in background…" : "Working…"
        };

        // A nested sub-agent belongs into the block of the agent that spawned it.
        AddMessage(subAgent, started.ParentSubAgentId);
        _subAgents[started.Id] = subAgent;
    }

    private void CompleteSubAgent(ChatSubAgentCompletedEvent completed)
    {
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

    private ChatMessageToolViewModel? FindToolMessage(string id)
    {
        return EnumerateAllMessages().OfType<ChatMessageToolViewModel>()
            .LastOrDefault(x => string.Equals(x.Id, id, StringComparison.Ordinal));
    }

    private void ShowEdit(AiEditViewModel? editViewModel)
    {
        if (editViewModel == null) return;

        _mainDockService.Show(editViewModel, DockShowLocation.Document);
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
    /// Schedules a throttled state save. Repeated requests within <see cref="AutoSaveInterval"/>
    /// are coalesced into the already scheduled save, so a streaming turn writes at most once per
    /// interval instead of once per delta.
    /// </summary>
    private void RequestSaveState()
    {
        if (!_initialized) return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RequestSaveState, DispatcherPriority.Background);
            return;
        }

        _autoSaveTimer ??= CreateAutoSaveTimer();

        if (_autoSaveTimer.IsEnabled) return;

        _autoSaveTimer.Start();
    }

    private DispatcherTimer CreateAutoSaveTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = AutoSaveInterval
        };
        timer.Tick += OnAutoSaveTick;
        return timer;
    }

    private void OnAutoSaveTick(object? sender, EventArgs e)
    {
        SaveState();
    }

    private void StopAutoSaveTimer()
    {
        _autoSaveTimer?.Stop();
    }

    public void SaveState()
    {
        StopAutoSaveTimer();

        if (SelectedChatService != null)
        {
            StoreCurrentMessages(SelectedChatService.Name, SelectedChatService);
        }

        var state = BuildChatState();

        try
        {
            var directory = Path.GetDirectoryName(_statePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using var stream = File.Open(_statePath, FileMode.Create, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, state, ChatStateSerializerOptions);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<Microsoft.Extensions.Logging.ILogger>()
                ?.Error("Saving chat state failed", e);
        }
    }

    private void LoadState()
    {
        LoadSessionHistoryIndex();

        if (File.Exists(_statePath))
        {
            try
            {
                using var stream = File.OpenRead(_statePath);
                var state = JsonSerializer.Deserialize<ChatState>(stream, ChatStateSerializerOptions);
                if (state != null)
                {
                    _selectedSessionByService.Clear();
                    foreach (var kvp in state.SelectedSessionByService)
                    {
                        if (!string.IsNullOrWhiteSpace(kvp.Value))
                            _selectedSessionByService[kvp.Key] = kvp.Value;
                    }

                    PruneAllSessionHistory();

                    // States from before the default service changed switch once to the new default.
                    var savedService = state.Version >= ChatState.CurrentVersion
                        ? ChatServices.FirstOrDefault(x => x.Name == state.SelectedChatServiceName)
                        : null;
                    SelectedChatService = savedService ?? ChatServices.FirstOrDefault();
                    return;
                }
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<Microsoft.Extensions.Logging.ILogger>()
                    ?.Warning("Loading chat state failed", e);
            }
        }

        PruneAllSessionHistory();

        SelectedChatService = ChatServices.FirstOrDefault();
    }

    /// <summary>
    /// Enforces the configured history limit for every known chat service. Called on load so a
    /// lowered limit also cleans up chats that were stored by a previous session.
    /// </summary>
    private void PruneAllSessionHistory()
    {
        foreach (var serviceName in _historyByService.Keys.ToArray())
        {
            _selectedSessionByService.TryGetValue(serviceName, out var protectedSessionId);
            PruneSessionHistory(serviceName, protectedSessionId);
        }
    }

    /// <summary>
    /// Deletes the oldest stored chats of a service once more than <see cref="MaxSessionHistoryKey"/>
    /// of them exist. The currently open session is never deleted.
    /// </summary>
    private void PruneSessionHistory(string serviceName, string? protectedSessionId)
    {
        var limit = GetMaxSessionHistory();
        if (limit <= 0) return;

        if (!_historyByService.TryGetValue(serviceName, out var items) || items.Count <= limit) return;

        items.Sort((a, b) => b.UpdatedAt.CompareTo(a.UpdatedAt));

        var kept = 0;
        var removed = new List<ChatSessionHistoryItem>();

        foreach (var item in items)
        {
            var isProtected = !string.IsNullOrWhiteSpace(protectedSessionId) &&
                              string.Equals(item.SessionId, protectedSessionId, StringComparison.Ordinal);

            if (kept < limit || isProtected)
            {
                ++kept;
                continue;
            }

            if (!TryDeleteSessionFile(item)) continue;

            removed.Add(item);
        }

        if (removed.Count == 0) return;

        foreach (var item in removed)
        {
            items.Remove(item);
            SessionHistory.Remove(item);
        }
    }

    private static bool TryDeleteSessionFile(ChatSessionHistoryItem item)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(item.FilePath) && File.Exists(item.FilePath))
                File.Delete(item.FilePath);

            return true;
        }
        catch (Exception e)
        {
            // Keep the entry so the file is retried instead of being orphaned in the index.
            ContainerLocator.Container.Resolve<Microsoft.Extensions.Logging.ILogger>()
                ?.Warning($"Deleting old chat '{item.Name}' failed", e);
            return false;
        }
    }

    private int GetMaxSessionHistory()
    {
        try
        {
            return _settingsService.GetSettingValue<int>(MaxSessionHistoryKey);
        }
        catch (Exception)
        {
            // The setting is not registered (e.g. in tests) — fall back to the default.
            return DefaultMaxSessionHistory;
        }
    }

    private ChatState BuildChatState()
    {
        return new ChatState
        {
            Version = ChatState.CurrentVersion,
            SelectedChatServiceName = SelectedChatService?.Name,
            SelectedSessionByService = _selectedSessionByService
        };
    }

    private static ChatMessageState? BuildMessageState(IChatMessage message)
    {
        switch (message)
        {
            case ChatMessageUserViewModel user:
                return new ChatMessageState(ChatMessageKind.User)
                {
                    Message = user.Message
                };
            case ChatMessageAssistantViewModel assistant:
                return new ChatMessageState(ChatMessageKind.Assistant)
                {
                    Content = assistant.Content,
                    Model = assistant.Model,
                    ShowModel = assistant.ShowModel
                };
            case ChatMessageReasoningViewModel reasoning:
                return new ChatMessageState(ChatMessageKind.Reasoning)
                {
                    Content = reasoning.Content
                };
            case ChatMessageToolViewModel tool:
                return new ChatMessageState(ChatMessageKind.Tool)
                {
                    Id = tool.Id,
                    ToolName = tool.ToolName,
                    ToolOutput = tool.ToolOutput,
                    IsSuccessful = tool.IsSuccessful
                };
            case ChatMessageErrorViewModel:
                // Error chat messages are intentionally not serialized.
                return null;
            case ChatMessageSkillViewModel skill:
                return new ChatMessageState(ChatMessageKind.Skill)
                {
                    SkillName = skill.SkillName,
                    Content = skill.Content
                };
            case ChatMessageSubAgentViewModel subAgent:
                return new ChatMessageState(ChatMessageKind.SubAgent)
                {
                    Id = subAgent.Id,
                    Message = subAgent.DisplayName,
                    Content = subAgent.Description,
                    Model = subAgent.Model,
                    ToolOutput = subAgent.StatusText,
                    IsSuccessful = subAgent.IsSuccessful,
                    Children = subAgent.Items.Select(BuildMessageState).OfType<ChatMessageState>().ToList()
                };
            default:
                return null;
        }
    }

    private static bool TryCreateMessage(ChatMessageState state, out IChatMessage message)
    {
        switch (state.Kind)
        {
            case ChatMessageKind.User:
                message = new ChatMessageUserViewModel(state.Message ?? string.Empty);
                return true;
            case ChatMessageKind.Assistant:
                message = new ChatMessageAssistantViewModel
                {
                    Content = state.Content ?? string.Empty,
                    IsStreaming = false,
                    Model = state.Model,
                    ShowModel = state.ShowModel
                };
                return true;
            case ChatMessageKind.Reasoning:
                message = new ChatMessageReasoningViewModel
                {
                    Content = state.Content ?? string.Empty,
                    IsStreaming = false
                };
                return true;
            case ChatMessageKind.Tool:
                if (string.IsNullOrWhiteSpace(state.ToolName))
                {
                    message = null!;
                    return false;
                }

                message = new ChatMessageToolViewModel(state.Id ?? Guid.NewGuid().ToString("N"), state.ToolName)
                {
                    ToolOutput = state.ToolOutput,
                    IsSuccessful = state.IsSuccessful,
                    IsToolRunning = false
                };
                return true;
            case ChatMessageKind.Skill:
                if (string.IsNullOrWhiteSpace(state.SkillName))
                {
                    message = null!;
                    return false;
                }

                message = new ChatMessageSkillViewModel(state.SkillName, state.Content ?? string.Empty);
                return true;
            case ChatMessageKind.SubAgent:
                if (string.IsNullOrWhiteSpace(state.Message))
                {
                    message = null!;
                    return false;
                }

                var restored = new ChatMessageSubAgentViewModel(state.Id ?? Guid.NewGuid().ToString("N"),
                    state.Message)
                {
                    Description = state.Content,
                    // Sessions written before the dedicated field kept the model in ToolName.
                    Model = state.Model ?? state.ToolName,
                    IsRunning = false,
                    IsExpanded = false,
                    IsSuccessful = state.IsSuccessful,
                    StatusText = state.ToolOutput ?? string.Empty
                };

                foreach (var childState in state.Children)
                {
                    if (TryCreateMessage(childState, out var child))
                        restored.Items.Add(child);
                }

                message = restored;
                return true;
            default:
                message = null!;
                return false;
        }
    }

    private void StoreCurrentMessages(string serviceName, IChatService? sourceService = null)
    {
        var messages = BuildCurrentMessageStates();
        if (messages.Count == 0) return;
        
        var sessionSource = sourceService ?? SelectedChatService;
        if (sessionSource is not IChatServiceWithSessions serviceWithSessions ||
            !string.Equals(sessionSource.Name, serviceName, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(serviceWithSessions.CurrentSessionId))
        {
            return;
        }

        _selectedSessionByService[serviceName] = serviceWithSessions.CurrentSessionId;
        SaveSessionHistory(serviceName, serviceWithSessions.CurrentSessionId, messages);
        PruneSessionHistory(serviceName, serviceWithSessions.CurrentSessionId);
    }

    private List<ChatMessageState> BuildCurrentMessageStates()
    {
        var messages = new List<ChatMessageState>(Messages.Count);
        foreach (var message in Messages)
        {
            var state = BuildMessageState(message);
            if (state != null) messages.Add(state);
        }

        return messages;
    }

    private void LoadMessagesForService(string serviceName)
    {
        LoadSessionHistoryForService(serviceName);

        if (_selectedSessionByService.TryGetValue(serviceName, out var selectedSessionId) &&
            TryGetHistoryItem(serviceName, selectedSessionId, out var selectedHistory))
        {
            SelectedSessionHistory = SessionHistory.FirstOrDefault(x =>
                string.Equals(x.SessionId, selectedHistory.SessionId, StringComparison.Ordinal));
            if (TryReadSessionMessages(selectedHistory, out var selectedStates))
            {
                LoadMessagesFromStates(selectedStates);
                return;
            }
        }

        if (SessionHistory.Count > 0)
        {
            var latest = SessionHistory[0];
            _selectedSessionByService[serviceName] = latest.SessionId;
            LoadSessionHistoryForService(serviceName);
            SelectedSessionHistory = SessionHistory.FirstOrDefault(x =>
                string.Equals(x.SessionId, latest.SessionId, StringComparison.Ordinal));
            if (TryReadSessionMessages(latest, out var latestStates))
            {
                LoadMessagesFromStates(latestStates);
                return;
            }
        }

        SelectedSessionHistory = null;

        Messages.Clear();
        _assistantMessagesById.Clear();
        _turnAssistantMessages.Clear();
        _assistantReasoningById.Clear();
        _subAgents.Clear();
        _pendingSubAgentTools.Clear();
    }

    private void LoadMessagesFromStates(IReadOnlyCollection<ChatMessageState> states)
    {
        Messages.Clear();
        _assistantMessagesById.Clear();
        _turnAssistantMessages.Clear();
        _assistantReasoningById.Clear();
        _subAgents.Clear();
        _pendingSubAgentTools.Clear();

        foreach (var messageState in states)
        {
            if (TryCreateMessage(messageState, out var message))
                Messages.Add(message);
        }
    }

    private async Task LoadSessionAsync(ChatSessionHistoryItem? item)
    {
        if (item == null || SelectedChatService == null) return;
        if (!string.Equals(item.ServiceName, SelectedChatService.Name, StringComparison.Ordinal)) return;
        if (SelectedChatService is IChatServiceWithSessions s && s.CurrentSessionId == item.SessionId) return;
        
        StoreCurrentMessages(SelectedChatService.Name, SelectedChatService);

        if (!IsInitialized) return;
        
        if (SelectedChatService is IChatServiceWithSessions serviceWithSessions)
        {
            var loaded = await serviceWithSessions.LoadSessionAsync(item.SessionId);
            if (!loaded)
            {
                AddErrorMessage($"Failed to load session '{item.SessionId}'.");
                return;
            }
        }

        _selectedSessionByService[item.ServiceName] = item.SessionId;
        
        if (TryReadSessionMessages(item, out var states))
        {
            LoadMessagesFromStates(states);
        }
        
        NotifyContentAdded();
    }

    private void LoadSessionHistoryIndex()
    {
        _historyByService.Clear();

        if (!Directory.Exists(_historyRootPath)) return;

        foreach (var serviceDirectory in Directory.EnumerateDirectories(_historyRootPath))
        {
            List<ChatSessionHistoryItem> items = [];
            foreach (var filePath in Directory.EnumerateFiles(serviceDirectory, "chat_*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    using var stream = File.OpenRead(filePath);
                    var file = JsonSerializer.Deserialize<ChatSessionFile>(stream, ChatStateSerializerOptions);
                    if (file == null || string.IsNullOrWhiteSpace(file.ServiceName) || string.IsNullOrWhiteSpace(file.SessionId))
                        continue;

                    items.Add(new ChatSessionHistoryItem
                    {
                        ServiceName = file.ServiceName,
                        SessionId = file.SessionId,
                        Name = string.IsNullOrWhiteSpace(file.Name) ? "Chat" : file.Name,
                        UpdatedAt = file.UpdatedAt,
                        FilePath = filePath
                    });
                }
                catch
                {
                    // Ignore malformed history files.
                }
            }

            if (items.Count == 0) continue;

            items = items
                .OrderByDescending(x => x.UpdatedAt)
                .ToList();

            _historyByService[items[0].ServiceName] = items;
        }
    }

    private void LoadSessionHistoryForService(string serviceName)
    {
        SessionHistory.Clear();

        if (!_historyByService.TryGetValue(serviceName, out var items)) return;

        foreach (var item in items.OrderByDescending(x => x.UpdatedAt))
        {
            SessionHistory.Add(item);
        }
    }

    private bool TryGetHistoryItem(string serviceName, string sessionId, out ChatSessionHistoryItem item)
    {
        item = null!;
        if (!_historyByService.TryGetValue(serviceName, out var items)) return false;

        var match = items.FirstOrDefault(x => string.Equals(x.SessionId, sessionId, StringComparison.Ordinal));
        if (match == null) return false;

        item = match;
        return true;
    }

    private void SaveSessionHistory(string serviceName, string sessionId, List<ChatMessageState> messages)
    {
        try
        {
            var serviceDirectory = GetServiceHistoryDirectory(serviceName);
            Directory.CreateDirectory(serviceDirectory);

            var existingFilePath = Directory.EnumerateFiles(serviceDirectory, $"chat_*_{sessionId}.json")
                .FirstOrDefault();

            var chatName = BuildChatName(messages);
            var safeName = SanitizeFileSegment(chatName);
            var targetFilePath = existingFilePath ?? Path.Combine(serviceDirectory, $"chat_{safeName}_{sessionId}.json");

            var createdAt = DateTimeOffset.UtcNow;
            if (existingFilePath != null)
            {
                try
                {
                    using var existingStream = File.OpenRead(existingFilePath);
                    var existing = JsonSerializer.Deserialize<ChatSessionFile>(existingStream, ChatStateSerializerOptions);
                    if (existing != null && existing.CreatedAt != default)
                    {
                        createdAt = existing.CreatedAt;
                    }
                }
                catch
                {
                    // Use current timestamp fallback.
                }
            }

            var file = new ChatSessionFile
            {
                ServiceName = serviceName,
                SessionId = sessionId,
                Name = chatName,
                CreatedAt = createdAt,
                UpdatedAt = DateTimeOffset.UtcNow,
                Messages = messages
            };

            using var stream = File.Open(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, file, ChatStateSerializerOptions);

            if (!_historyByService.TryGetValue(serviceName, out var items))
            {
                items = [];
                _historyByService[serviceName] = items;
            }

            var existingItem = items.FirstOrDefault(x => string.Equals(x.SessionId, sessionId, StringComparison.Ordinal));
            if (existingItem == null)
            {
                items.Add(new ChatSessionHistoryItem
                {
                    ServiceName = serviceName,
                    SessionId = sessionId,
                    Name = chatName,
                    UpdatedAt = file.UpdatedAt,
                    FilePath = targetFilePath
                });
            }
            else
            {
                existingItem.Name = chatName;
                existingItem.UpdatedAt = file.UpdatedAt;
                existingItem.FilePath = targetFilePath;
            }

            items.Sort((a, b) => b.UpdatedAt.CompareTo(a.UpdatedAt));
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<Microsoft.Extensions.Logging.ILogger>()
                ?.Warning("Saving chat history failed", e);
        }
    }

    private string GetServiceHistoryDirectory(string serviceName)
    {
        return Path.Combine(_historyRootPath, SanitizeFileSegment(serviceName));
    }

    private static string BuildChatName(IReadOnlyCollection<ChatMessageState> messages)
    {
        var firstUserMessage = messages.FirstOrDefault(x => x.Kind == ChatMessageKind.User)?.Message;
        if (string.IsNullOrWhiteSpace(firstUserMessage))
        {
            return "Chat";
        }

        var normalized = Regex.Replace(firstUserMessage.Trim(), "\\s+", " ");
        return normalized.Length > 64 ? normalized[..64].Trim() : normalized;
    }

    private static string SanitizeFileSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "chat";

        var sanitized = Regex.Replace(value.Trim(), "[^a-zA-Z0-9]+", "_")
            .Trim('_')
            .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(sanitized))
            return "chat";

        if (sanitized.Length > 64)
            return sanitized[..64];

        return sanitized;
    }

    private void UpdateSelectedSessionFromService(IChatService chatService)
    {
        if (chatService is not IChatServiceWithSessions serviceWithSessions ||
            string.IsNullOrWhiteSpace(serviceWithSessions.CurrentSessionId))
        {
            return;
        }
        
        _selectedSessionByService[chatService.Name] = serviceWithSessions.CurrentSessionId;
        LoadSessionHistoryForService(chatService.Name);
        SessionHistory.Insert(0, new ChatSessionHistoryItem()
        {
            FilePath = "",
            Name = "New Session",
            ServiceName = chatService.Name,
            SessionId = serviceWithSessions.CurrentSessionId,
            UpdatedAt = DateTimeOffset.Now
        });
        SelectedSessionHistory = SessionHistory.FirstOrDefault(x =>
            string.Equals(x.SessionId, serviceWithSessions.CurrentSessionId, StringComparison.Ordinal));
    }

    private static bool TryReadSessionMessages(ChatSessionHistoryItem item, out List<ChatMessageState> messages)
    {
        messages = [];
        try
        {
            if (!File.Exists(item.FilePath)) return false;

            using var stream = File.OpenRead(item.FilePath);
            var file = JsonSerializer.Deserialize<ChatSessionFile>(stream, ChatStateSerializerOptions);
            if (file == null) return false;

            messages = file.Messages;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class ChatState
    {
        /// <summary>Version 1: OneWare Cloud became the default chat service.</summary>
        public const int CurrentVersion = 1;

        public int Version { get; set; }

        public string? SelectedChatServiceName { get; set; }

        public Dictionary<string, string> SelectedSessionByService { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class ChatSessionFile
    {
        public string ServiceName { get; set; } = string.Empty;

        public string SessionId { get; set; } = string.Empty;

        public string Name { get; set; } = "Chat";

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public List<ChatMessageState> Messages { get; set; } = [];
    }

    public sealed class ChatSessionHistoryItem
    {
        public required string ServiceName { get; init; }

        public required string SessionId { get; init; }

        public required string Name { get; set; }

        public required DateTimeOffset UpdatedAt { get; set; }

        public required string FilePath { get; set; }

        public string DisplayName => Name;

        public string UpdatedAtLabel => UpdatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
    }

    private sealed class ChatMessageState
    {
        public ChatMessageState()
        {
        }

        public ChatMessageState(ChatMessageKind kind)
        {
            Kind = kind;
        }

        public ChatMessageKind Kind { get; set; }
        public string? Message { get; set; }
        public string? Content { get; set; }
        public string? Id { get; set; }
        public string? ToolName { get; set; }
        public string? ToolOutput { get; set; }
        public string? SkillName { get; set; }

        /// <summary>Model that produced the message, or that a sub-agent ran with.</summary>
        public string? Model { get; set; }

        /// <summary>Whether the message is the one that ended its turn and therefore names its model.</summary>
        public bool ShowModel { get; set; }
        public bool IsSuccessful { get; set; }

        /// <summary>Messages nested inside a sub-agent block.</summary>
        public List<ChatMessageState> Children { get; set; } = [];
    }

    private enum ChatMessageKind
    {
        User,
        Assistant,
        Reasoning,
        Tool,
        Skill,
        SubAgent
    }
}
