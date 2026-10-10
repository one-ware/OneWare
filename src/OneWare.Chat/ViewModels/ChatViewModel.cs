using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using OneWare.Chat.Services;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Chat.ViewModels;

/// <summary>
/// The AI chat panel. Shows one chat at a time and an overview of all chats; every chat talks to its own session of
/// a chat service, so working chats keep running in the background.
/// </summary>
public class ChatViewModel : ExtendedTool, IChatManagerService, IChatSessionHost
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
    private readonly string _statePath;
    private readonly string _historyRootPath;

    private readonly Dictionary<string, List<ChatSessionHistoryItem>> _historyByService = new(StringComparer.Ordinal);

    private bool _initialized;
    private string? _draftText;

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

        NewChatCommand = new RelayCommand(() => OpenNewChat());
        ShowOverviewCommand = new RelayCommand(ShowOverview);

        applicationStateService.RegisterShutdownAction(SaveState);
    }

    public ObservableCollection<IChatService> ChatServices { get; } = [];

    /// <summary>
    /// Conversations kept in memory: the shown one and those still working in the background. Idle chats are
    /// unloaded when you leave them and live on in the history.
    /// </summary>
    internal IReadOnlyList<ChatSessionViewModel> LoadedSessions => _sessions;

    private readonly List<ChatSessionViewModel> _sessions = [];

    // The chat restored on the next start, also while the overview is shown.
    private (string ServiceName, string? SessionId)? _lastChat;

    /// <summary>The chat shown in the panel; null while the overview is shown.</summary>
    public ChatSessionViewModel? CurrentSession
    {
        get;
        private set
        {
            var old = field;
            if (!SetProperty(ref field, value)) return;

            if (old != null) old.IsSelected = false;
            if (value != null)
            {
                value.IsSelected = true;
                _lastChat = (value.RegisteredService.Name, value.RememberedSessionId ?? value.CurrentSessionId);
                _ = value.EnsureActivatedAsync();
            }

            OnPropertyChanged(nameof(IsOverviewVisible));
            OnPropertyChanged(nameof(ComposerSession));
            OnPropertyChanged(nameof(SelectedChatService));
            RefreshOverview();
            RequestSave();
        }
    }

    public bool IsOverviewVisible => CurrentSession == null;

    /// <summary>
    /// New chat behind the input box while the overview is shown; sending from it opens it.
    /// </summary>
    public ChatSessionViewModel? DraftSession
    {
        get;
        private set
        {
            if (!SetProperty(ref field, value)) return;
            OnPropertyChanged(nameof(ComposerSession));
            OnPropertyChanged(nameof(SelectedChatService));
        }
    }

    /// <summary>The chat the input box sends to: the shown chat, or the new chat on the overview.</summary>
    public ChatSessionViewModel? ComposerSession => CurrentSession ?? DraftSession;

    /// <summary>
    /// Selectable chat agents ("Agent", "Plan", "Ask" and custom ones), bound by the agent selector.
    /// </summary>
    public IChatAgentService AgentService { get; }

    /// <summary>All chats, working and stored ones, newest first.</summary>
    public ObservableCollection<ChatOverviewItem> Overview { get; } = [];

    /// <summary>Selecting a chat on the overview opens it.</summary>
    public ChatOverviewItem? SelectedOverviewItem
    {
        get;
        set
        {
            if (!SetProperty(ref field, value) || value == null) return;

            _ = OpenOverviewItemAsync(value);
            Dispatcher.UIThread.Post(() => SelectedOverviewItem = null);
        }
    }

    /// <summary>
    /// Chat service of the shown chat. Setting another service switches the shown chat to it; a working chat keeps
    /// going in the background and the service opens in a new chat instead.
    /// </summary>
    public IChatService? SelectedChatService
    {
        get => ComposerSession?.RegisteredService;
        set
        {
            if (value == null || ReferenceEquals(ComposerSession?.RegisteredService, value)) return;

            if (CurrentSession == null)
            {
                // On the overview the service picker chooses the service of the next chat.
                var text = DraftSession?.CurrentMessage;
                DiscardDraft();
                CreateDraft(value, text);
                return;
            }

            OpenNewChat(value);
        }
    }

    public AiFileEditService AiFileEditService { get; }

    public RelayCommand<AiEditViewModel> ShowEditCommand => new(ShowEdit);

    /// <summary>Starts a new chat with the service of the shown (or last) chat.</summary>
    public RelayCommand NewChatCommand { get; }

    /// <summary>Leaves the shown chat for the list of chats.</summary>
    public RelayCommand ShowOverviewCommand { get; }

    public override void InitializeContent()
    {
        if (_initialized) return;
        _initialized = true;

        LoadState();
    }

    public void RegisterChatService(IChatService chatService)
    {
        ChatServices.Add(chatService);

        if (chatService is IChatServiceWithHistoryReset historyReset)
            historyReset.HistoryCleared += OnServiceHistoryCleared;

        // Services registered after the panel was restored still need a chat to be usable.
        if (_initialized && CurrentSession == null && DraftSession == null)
            OpenNewChat(chatService);
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private IChatService? DefaultService =>
        ComposerSession?.RegisteredService ??
        ChatServices.FirstOrDefault(x => string.Equals(x.Name, _lastChat?.ServiceName, StringComparison.Ordinal)) ??
        ChatServices.FirstOrDefault();

    /// <summary>
    /// Creates a chat for a service: on the registered instance while no loaded chat uses it, otherwise on a new
    /// parallel session. Services without parallel sessions run one chat at a time; their idle chat is unloaded
    /// first, a working one is returned instead (null when that happens).
    /// </summary>
    private ChatSessionViewModel? CreateSession(IChatService registered, string? sessionId,
        IReadOnlyCollection<ChatMessageState>? states, string? title, out ChatSessionViewModel? blocking)
    {
        blocking = null;
        IChatService? instance = null;

        if (_sessions.All(t => !ReferenceEquals(t.Service, registered)))
            instance = registered;
        else if (registered is IChatServiceWithParallelSessions parallel)
            instance = parallel.CreateSession();
        else
        {
            var holder = _sessions.First(t => ReferenceEquals(t.Service, registered));
            if (holder.IsBusy)
            {
                blocking = holder;
                return null;
            }

            Unload(holder);
            instance = registered;
        }

        var session = new ChatSessionViewModel(registered, instance, this, _aiFunctionProvider, AgentService)
        {
            RememberedSessionId = sessionId
        };

        if (states is { Count: > 0 }) session.LoadMessagesFromStates(states);
        if (!string.IsNullOrWhiteSpace(title)) session.Title = title;
        session.IsDirty = false;
        session.PropertyChanged += OnSessionPropertyChanged;
        _sessions.Add(session);
        return session;
    }

    private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChatSessionViewModel.Title) or nameof(ChatSessionViewModel.IsBusy))
            RefreshOverview();
    }

    /// <summary>Stores the chat in the history and releases it.</summary>
    private void Unload(ChatSessionViewModel session)
    {
        StoreTranscript(session);

        _sessions.Remove(session);
        if (ReferenceEquals(DraftSession, session)) DraftSession = null;
        session.PropertyChanged -= OnSessionPropertyChanged;
        session.IsSelected = false;
        session.Detach();

        // Parallel sessions belong to the chat; the registered service stays for the next one.
        if (!ReferenceEquals(session.Service, session.RegisteredService)) _ = DisposeInstanceAsync(session.Service);
    }

    /// <summary>Leaves the shown chat. Idle chats are unloaded, working ones keep running in the background.</summary>
    private void LeaveCurrent()
    {
        var current = CurrentSession;
        if (current == null) return;

        CurrentSession = null;
        if (!current.IsBusy) Unload(current);
    }

    private void Show(ChatSessionViewModel session)
    {
        if (ReferenceEquals(CurrentSession, session)) return;

        LeaveCurrent();
        if (ReferenceEquals(DraftSession, session))
            DraftSession = null;
        else
            DiscardDraft();
        CurrentSession = session;
        SaveStateFile();
    }

    private void ShowOverview()
    {
        var service = CurrentSession?.RegisteredService ?? DefaultService;
        LeaveCurrent();
        if (DraftSession == null && service != null) CreateDraft(service, null);
        RefreshOverview();
        SaveStateFile();
    }

    /// <summary>Prepares the new chat the input box on the overview sends to.</summary>
    private void CreateDraft(IChatService service, string? text)
    {
        var draft = CreateSession(service, null, null, null, out _);
        if (draft == null) return;

        text ??= _draftText;
        _draftText = null;
        if (!string.IsNullOrEmpty(text)) draft.CurrentMessage = text;
        DraftSession = draft;
        _ = draft.EnsureActivatedAsync();
    }

    private void DiscardDraft()
    {
        if (DraftSession is not { } draft) return;

        // Unsent overview text stays for the next time the overview is shown.
        _draftText = draft.CurrentMessage;
        Unload(draft);
    }

    /// <summary>Opens a new, empty chat and shows it.</summary>
    public ChatSessionViewModel? OpenNewChat(IChatService? service = null)
    {
        service ??= DefaultService;
        if (service == null) return null;

        // An empty chat of the same service is reused instead of stacking up empty chats.
        if (CurrentSession is { } current && ReferenceEquals(current.RegisteredService, service) &&
            current.Messages.Count == 0 && !current.IsBusy)
            return current;

        if (DraftSession is { } draft && ReferenceEquals(draft.RegisteredService, service))
        {
            Show(draft);
            return draft;
        }

        LeaveCurrent();
        DiscardDraft();

        var session = CreateSession(service, null, null, null, out var blocking);
        if (session == null)
        {
            // The service runs one chat at a time and that chat is still working.
            if (blocking != null) Show(blocking);
            return blocking;
        }

        Show(session);
        return session;
    }

    private static async Task DisposeInstanceAsync(IChatService instance)
    {
        try
        {
            await instance.DisposeAsync();
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<Microsoft.Extensions.Logging.ILogger>()
                ?.Warning("Closing chat session failed", e);
        }
    }

    private ChatSessionViewModel? FindLoaded(string serviceName, string sessionId) =>
        _sessions.FirstOrDefault(t =>
            string.Equals(t.RegisteredService.Name, serviceName, StringComparison.Ordinal) &&
            (string.Equals(t.CurrentSessionId, sessionId, StringComparison.Ordinal) ||
             string.Equals(t.RememberedSessionId, sessionId, StringComparison.Ordinal)));

    private async Task OpenOverviewItemAsync(ChatOverviewItem item)
    {
        if (item.Session != null && _sessions.Contains(item.Session))
        {
            Show(item.Session);
            return;
        }

        if (item.HistoryItem != null) await OpenHistoryItemAsync(item.HistoryItem);
    }

    private async Task OpenHistoryItemAsync(ChatSessionHistoryItem item)
    {
        if (FindLoaded(item.ServiceName, item.SessionId) is { } loaded)
        {
            Show(loaded);
            return;
        }

        var service = ChatServices.FirstOrDefault(s => string.Equals(s.Name, item.ServiceName, StringComparison.Ordinal));
        if (service == null) return;

        TryReadSessionMessages(item, out var states);

        LeaveCurrent();
        var session = CreateSession(service, item.SessionId, states, item.Name, out var blocking);
        if (session == null)
        {
            if (blocking != null) Show(blocking);
            return;
        }

        Show(session);
        await session.EnsureActivatedAsync();
    }

    /// <summary>
    /// Brings the overview list in line with the loaded chats and the history, updating rows in place so the list
    /// keeps its scroll position.
    /// </summary>
    private void RefreshOverview()
    {
        var rows = new List<(string Key, ChatSessionViewModel? Session, ChatSessionHistoryItem? History, string Title,
            string ServiceName, DateTimeOffset UpdatedAt)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var session in _sessions)
        {
            var sessionId = session.RememberedSessionId ?? session.CurrentSessionId;
            if (string.IsNullOrWhiteSpace(sessionId) ||
                (session.Messages.Count == 0 && !session.IsBusy)) continue;

            var key = ChatOverviewItem.BuildKey(session.RegisteredService.Name, sessionId);
            if (!seen.Add(key)) continue;

            TryGetHistoryItem(session.RegisteredService.Name, sessionId, out var history);
            rows.Add((key, session, history, session.Title, session.RegisteredService.Name,
                history?.UpdatedAt ?? session.CreatedAt));
        }

        foreach (var history in _historyByService.Values.SelectMany(x => x))
        {
            var key = ChatOverviewItem.BuildKey(history.ServiceName, history.SessionId);
            if (!seen.Add(key)) continue;

            rows.Add((key, null, history, history.Name, history.ServiceName, history.UpdatedAt));
        }

        rows.Sort((x, y) =>
        {
            // Working chats stay on top so they are easy to find again.
            var busy = (y.Session?.IsBusy ?? false).CompareTo(x.Session?.IsBusy ?? false);
            return busy != 0 ? busy : y.UpdatedAt.CompareTo(x.UpdatedAt);
        });

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var existingIndex = -1;
            for (var j = i; j < Overview.Count; j++)
            {
                if (!string.Equals(Overview[j].Key, row.Key, StringComparison.Ordinal)) continue;
                existingIndex = j;
                break;
            }

            ChatOverviewItem item;
            if (existingIndex < 0)
            {
                item = new ChatOverviewItem(row.Key);
                Overview.Insert(i, item);
            }
            else
            {
                if (existingIndex != i) Overview.Move(existingIndex, i);
                item = Overview[i];
            }

            item.Session = row.Session;
            item.HistoryItem = row.History;
            item.Title = row.Title;
            item.ServiceName = row.ServiceName;
            item.UpdatedAt = row.UpdatedAt;
        }

        while (Overview.Count > rows.Count) Overview.RemoveAt(Overview.Count - 1);
    }

    private HashSet<string> GetOpenSessionIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var session in _sessions)
        {
            if (!string.IsNullOrWhiteSpace(session.RememberedSessionId)) ids.Add(session.RememberedSessionId);
            if (!string.IsNullOrWhiteSpace(session.CurrentSessionId)) ids.Add(session.CurrentSessionId);
        }

        return ids;
    }

    // ── IChatSessionHost ──────────────────────────────────────────────────────

    void IChatSessionHost.RequestSave(ChatSessionViewModel session) => RequestSave();

    void IChatSessionHost.SaveNow() => SaveState();

    void IChatSessionHost.OnSending(ChatSessionViewModel session)
    {
        if (ReferenceEquals(session, DraftSession)) Show(session);
    }

        void IChatSessionHost.OnSessionChanged(ChatSessionViewModel session)
    {
        if (!string.IsNullOrWhiteSpace(session.CurrentSessionId))
            session.RememberedSessionId = session.CurrentSessionId;

        if (ReferenceEquals(session, CurrentSession))
            _lastChat = (session.RegisteredService.Name, session.RememberedSessionId);

        RefreshOverview();
        RequestSave();
    }

    /// <summary>Stores the transcript of a chat in the history of its service.</summary>
    public void StoreTranscript(ChatSessionViewModel session)
    {
        // Until a chat is activated its service may still hold the session of another chat.
        if (!session.IsActivated) return;

        var sessionId = session.CurrentSessionId;
        if (string.IsNullOrWhiteSpace(sessionId)) return;

        var messages = session.BuildMessageStates();
        session.IsDirty = false;
        if (messages.Count == 0) return;

        var serviceName = session.RegisteredService.Name;
        session.RememberedSessionId = sessionId;
        SaveSessionHistory(serviceName, sessionId, messages);
        PruneSessionHistory(serviceName);
        RefreshOverview();
    }

    // ── Function events ───────────────────────────────────────────────────────

    /// <summary>
    /// Finds the chat a OneWare function call belongs to: by session, then by an existing tool message, then the only
    /// working chat, and finally the shown chat.
    /// </summary>
    internal ChatSessionViewModel? FindTabForFunction(string? sessionId, string? functionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var bySession = _sessions.FirstOrDefault(t =>
                string.Equals(t.CurrentSessionId, sessionId, StringComparison.Ordinal));
            if (bySession != null) return bySession;
        }

        if (!string.IsNullOrWhiteSpace(functionId))
        {
            var byTool = _sessions.FirstOrDefault(t => t.FindToolMessage(functionId) != null);
            if (byTool != null) return byTool;
        }

        var busy = _sessions.Where(t => t.IsBusy).Take(2).ToList();
        return busy.Count == 1 ? busy[0] : CurrentSession;
    }

    private void OnFunctionStarted(object? sender, AiFunctionStartedEvent function) =>
        FindTabForFunction(function.SessionId, null)?.HandleFunctionStarted(function);

    private void OnFunctionCompleted(object? sender, AiFunctionCompletedEvent function) =>
        FindTabForFunction(function.SessionId, function.Id)?.HandleFunctionCompleted(function);

    private void OnFunctionProgress(object? sender, AiFunctionProgressEvent progress) =>
        FindTabForFunction(progress.SessionId, progress.Id)?.HandleFunctionProgress(progress);

    private void ShowEdit(AiEditViewModel? editViewModel)
    {
        if (editViewModel == null) return;

        _mainDockService.Show(editViewModel, DockShowLocation.Document);
    }

    // ── History reset ─────────────────────────────────────────────────────────

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

        // Drop the visible transcripts too, otherwise they would be saved into the new account's sessions.
        foreach (var session in _sessions.Where(t => string.Equals(t.RegisteredService.Name, serviceName, StringComparison.Ordinal)))
            session.ClearTranscript();

        RefreshOverview();

        if (_initialized) SaveState();
    }

    // ── Saving ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Schedules a throttled state save. Repeated requests within <see cref="AutoSaveInterval"/>
    /// are coalesced into the already scheduled save, so a streaming turn writes at most once per
    /// interval instead of once per delta.
    /// </summary>
    private void RequestSave()
    {
        if (!_initialized) return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RequestSave, DispatcherPriority.Background);
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

        foreach (var session in _sessions.Where(t => t.IsDirty).ToList())
            StoreTranscript(session);

        SaveStateFile();
    }

    private void SaveStateFile()
    {
        if (!_initialized) return;

        var current = CurrentSession;
        var last = current != null
            ? (current.RegisteredService.Name, current.RememberedSessionId ?? current.CurrentSessionId)
            : _lastChat;

        var state = new ChatState
        {
            Version = ChatState.CurrentVersion,
            LastServiceName = last?.Item1,
            LastSessionId = last?.Item2
        };

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

        ChatState? state = null;
        if (File.Exists(_statePath))
        {
            try
            {
                using var stream = File.OpenRead(_statePath);
                state = JsonSerializer.Deserialize<ChatState>(stream, ChatStateSerializerOptions);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<Microsoft.Extensions.Logging.ILogger>()
                    ?.Warning("Loading chat state failed", e);
            }
        }

        var last = ResolveLastChat(state, ChatServices.Select(x => x.Name).ToList(),
            serviceName => _historyByService.TryGetValue(serviceName, out var items)
                ? items.OrderByDescending(x => x.UpdatedAt).FirstOrDefault()?.SessionId
                : null);

        foreach (var serviceName in _historyByService.Keys.ToArray())
            PruneSessionHistory(serviceName);

        if (last is not { } chat)
        {
            RefreshOverview();
            return;
        }

        var service = ChatServices.First(s => string.Equals(s.Name, chat.ServiceName, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(chat.SessionId) && TryGetHistoryItem(service.Name, chat.SessionId, out var item))
        {
            TryReadSessionMessages(item, out var messages);
            var restored = CreateSession(service, chat.SessionId, messages, item.Name, out _);
            if (restored != null)
            {
                Show(restored);
                return;
            }
        }

        OpenNewChat(service);
    }

    /// <summary>
    /// Works out which chat to show on start. States from before version 2 open the previously selected service
    /// with its last chat; version 0 states switch once to the default service.
    /// </summary>
    internal static (string ServiceName, string? SessionId)? ResolveLastChat(ChatState? state,
        IReadOnlyList<string> serviceNames, Func<string, string?> latestSessionOf)
    {
        if (serviceNames.Count == 0) return null;

        bool IsKnown(string? name) => name != null && serviceNames.Contains(name, StringComparer.Ordinal);

        if (state is { Version: >= 2 } && IsKnown(state.LastServiceName))
            return (state.LastServiceName!, string.IsNullOrWhiteSpace(state.LastSessionId) ? null : state.LastSessionId);

        var serviceName = state is { Version: 1 } && IsKnown(state.SelectedChatServiceName)
            ? state.SelectedChatServiceName!
            : serviceNames[0];

        string? sessionId = null;
        if (state?.SelectedSessionByService.TryGetValue(serviceName, out var remembered) == true &&
            !string.IsNullOrWhiteSpace(remembered))
            sessionId = remembered;
        sessionId ??= latestSessionOf(serviceName);

        return (serviceName, sessionId);
    }

    // ── History ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Deletes the oldest stored chats of a service once more than <see cref="MaxSessionHistoryKey"/>
    /// of them exist. Loaded chats are never deleted.
    /// </summary>
    private void PruneSessionHistory(string serviceName)
    {
        var limit = GetMaxSessionHistory();
        if (limit <= 0) return;

        if (!_historyByService.TryGetValue(serviceName, out var items) || items.Count <= limit) return;

        items.Sort((a, b) => b.UpdatedAt.CompareTo(a.UpdatedAt));

        var protectedSessionIds = GetOpenSessionIds();
        var kept = 0;
        var removed = new List<ChatSessionHistoryItem>();

        foreach (var item in items)
        {
            var isProtected = protectedSessionIds.Contains(item.SessionId);

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

            var chatName = ChatTranscript.BuildChatName(messages);
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

    internal sealed class ChatState
    {
        /// <summary>
        /// Version 1: OneWare Cloud became the default chat service. Version 2: the last shown chat is stored.
        /// </summary>
        public const int CurrentVersion = 2;

        public int Version { get; set; }

        /// <summary>Service of the chat shown last.</summary>
        public string? LastServiceName { get; set; }

        /// <summary>Session of the chat shown last; null for a new chat.</summary>
        public string? LastSessionId { get; set; }

        /// <summary>Version 1 and older: the selected service.</summary>
        public string? SelectedChatServiceName { get; set; }

        /// <summary>Version 1 and older: the last session of each service.</summary>
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
}
