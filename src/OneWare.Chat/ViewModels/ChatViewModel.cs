using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    private readonly IMainDockService _mainDockService;
    private readonly string _statePath;
    private readonly string _historyDirectoryPath;

    private readonly Dictionary<string, ChatMessageAssistantViewModel> _assistantMessagesById =
        new(StringComparer.Ordinal);

    private readonly Dictionary<string, ChatMessageReasoningViewModel> _assistantReasoningById =
        new(StringComparer.Ordinal);

    private ChatHistorySessionState? _activeHistorySession;
    private string? _activeHistorySessionId;
    private bool _isLoadingExplicitHistory;
    private bool _initialized;

    private static readonly JsonSerializerOptions ChatStateSerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ChatViewModel(IAiFunctionProvider aiFunctionProvider, IMainDockService mainDockService,
        AiFileEditService aiFileEditService, IPaths paths,
        IApplicationStateService applicationStateService) : base(IconKey)
    {
        Id = "AI_Chat";
        Title = "AI Chat";

        aiFunctionProvider.FunctionStarted += OnFunctionStarted;
        aiFunctionProvider.FunctionPermissionRequested += OnFunctionPermissionRequested;
        aiFunctionProvider.FunctionCompleted += OnFunctionCompleted;

        _mainDockService = mainDockService;
        _statePath = Path.Combine(paths.AppDataDirectory, "Chat", "ChatState.json");
        _historyDirectoryPath = Path.Combine(paths.AppDataDirectory, "Chat", "History");
        AiFileEditService = aiFileEditService;

        NewChatCommand = new AsyncRelayCommand(NewChatAsync);
        SendCommand = new AsyncRelayCommand(SendAsync, CanSend);
        AbortCommand = new AsyncRelayCommand(AbortAsync, CanAbort);
        InitializeCurrentCommand = new AsyncRelayCommand(InitializeCurrentAsync);
        LoadHistoryCommand = new AsyncRelayCommand<ChatHistoryItem?>(LoadHistoryAsync);

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
                AbortCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get;
        set => SetProperty(ref field, value);
    } = "Starting...";

    public ObservableCollection<IChatMessage> Messages { get; set; } = new();

    public ObservableCollection<IChatService> ChatServices { get; } = [];

    public ObservableCollection<ChatHistoryItem> HistoryItems { get; } = [];

    public IChatService? SelectedChatService
    {
        get;
        set
        {
            var oldValue = field;
            if (SetProperty(ref field, value))
            {
                PersistActiveSession();

                if (oldValue != null)
                {
                    oldValue.EventReceived -= OnEventReceived;
                    oldValue.StatusChanged -= OnStatusChanged;
                    oldValue.SessionReset -= OnSessionReset;
                }

                if (value != null)
                {
                    value.EventReceived += OnEventReceived;
                    value.StatusChanged += OnStatusChanged;
                    value.SessionReset += OnSessionReset;

                    if (_isLoadingExplicitHistory &&
                        _activeHistorySession?.ChatServiceName == value.Name)
                    {
                        LoadMessagesFromStates(_activeHistorySession.Messages);
                    }
                    else
                    {
                        EnsureActiveSessionForService(value.Name);
                    }

                    _ = InitializeCurrentAsync();
                }
            }
        }
    }

    public AiFileEditService AiFileEditService { get; }

    public RelayCommand<AiEditViewModel> ShowEditCommand => new(ShowEdit);

    public AsyncRelayCommand NewChatCommand { get; }

    public AsyncRelayCommand SendCommand { get; }

    public AsyncRelayCommand AbortCommand { get; }

    public AsyncRelayCommand InitializeCurrentCommand { get; }

    public AsyncRelayCommand<ChatHistoryItem?> LoadHistoryCommand { get; }

    public override void InitializeContent()
    {
        if (_initialized) return;
        _initialized = true;

        LoadState();
    }

    private async Task<bool> InitializeCurrentAsync()
    {
        if (SelectedChatService == null) return false;

        _assistantMessagesById.Clear();

        var status = await SelectedChatService.InitializeAsync();

        IsInitialized = status;
        CaptureCurrentChatId();

        return status;
    }

    private async Task NewChatAsync()
    {
        PersistActiveSession();

        if (SelectedChatService != null)
        {
            if (!IsInitialized)
            {
                await InitializeCurrentAsync();
            }

            await AbortAsync();
            await SelectedChatService.NewChatAsync();
        }

        Messages.Clear();
        _assistantMessagesById.Clear();
        _assistantReasoningById.Clear();

        if (SelectedChatService != null)
        {
            CreateNewActiveSession(SelectedChatService.Name);
            CaptureCurrentChatId();
            PersistActiveSession();
        }
    }

    private async Task SendAsync()
    {
        var prompt = CurrentMessage.Trim();
        if (string.IsNullOrWhiteSpace(prompt)) return;

        if (SelectedChatService == null)
        {
            Messages.Add(new ChatMessageAssistantViewModel()
            {
                Content = "No ChatService Selected"
            });
            return;
        }

        EnsureActiveSessionForService(SelectedChatService.Name);

        if (!IsInitialized)
        {
            await InitializeCurrentAsync();
        }

        if (!IsConnected)
        {
            Messages.Add(new ChatMessageAssistantViewModel()
            {
                Content = $"{SelectedChatService.Name} is not connected yet."
            });
            return;
        }

        var userMessage = new ChatMessageUserViewModel(prompt);
        var assistantMessage = new ChatMessageAssistantViewModel("init")
        {
            IsStreaming = true
        };

        AddMessage(userMessage);
        AddMessage(assistantMessage);

        CurrentMessage = string.Empty;
        IsBusy = true;

        ContentAdded?.Invoke(this, EventArgs.Empty);

        try
        {
            await SelectedChatService.SendAsync(prompt);
        }
        catch (Exception ex)
        {
            assistantMessage.Content = ex.Message;
            assistantMessage.IsStreaming = false;
            IsBusy = false;
            PersistActiveSession();
        }
    }

    private async Task AbortAsync()
    {
        if (SelectedChatService == null) return;

        try
        {
            await SelectedChatService.AbortAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSend() => IsConnected && !IsBusy && !string.IsNullOrWhiteSpace(CurrentMessage);

    private bool CanAbort() => IsConnected && IsBusy;

    private void AddMessage(IChatMessage message)
    {
        if (Messages.LastOrDefault() is ChatMessageAssistantViewModel { MessageId: "init" } initMessage)
        {
            Messages.Remove(initMessage);
        }

        Messages.Add(message);
    }

    private ChatMessageReasoningViewModel GetOrCreateAssistantReasoningMessage(string? reasoningId)
    {
        if (!string.IsNullOrWhiteSpace(reasoningId))
        {
            if (_assistantReasoningById.TryGetValue(reasoningId, out var existing))
                return existing;

            var created = new ChatMessageReasoningViewModel(reasoningId);
            AddMessage(created);
            _assistantReasoningById[reasoningId] = created;
            return created;
        }

        var activeReasoning = new ChatMessageReasoningViewModel(reasoningId);
        AddMessage(activeReasoning);

        return activeReasoning;
    }

    private ChatMessageAssistantViewModel GetOrCreateAssistantMessage(string? messageId)
    {
        if (Messages.LastOrDefault() is ChatMessageAssistantViewModel { MessageId: "init" } initMessage)
        {
            Messages.Remove(initMessage);
        }

        if (!string.IsNullOrWhiteSpace(messageId))
        {
            if (_assistantMessagesById.TryGetValue(messageId, out var existing))
                return existing;

            var created = new ChatMessageAssistantViewModel(messageId);
            AddMessage(created);
            _assistantMessagesById[messageId] = created;
            return created;
        }

        var activeAssistantMessage = new ChatMessageAssistantViewModel(messageId);
        AddMessage(activeAssistantMessage);

        return activeAssistantMessage;
    }

    private void FinishTurn()
    {
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var message in Messages.OfType<ChatMessageAssistantViewModel>())
                message.IsStreaming = false;

            foreach (var message in Messages.OfType<ChatMessageReasoningViewModel>())
                message.IsStreaming = false;

            IsBusy = false;
            PersistActiveSession();
        });
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
                    var message = GetOrCreateAssistantMessage(x.MessageId);
                    message.IsStreaming = true;
                    message.Content += x.Content;
                    ContentAdded?.Invoke(this, EventArgs.Empty);
                });
                break;
            }
            case ChatMessageEvent x:
            {
                if (string.IsNullOrWhiteSpace(x.Content)) break;
                Dispatcher.UIThread.Post(() =>
                {
                    var message = GetOrCreateAssistantMessage(x.MessageId);
                    message.Content = x.Content;
                    message.IsStreaming = false;
                    ContentAdded?.Invoke(this, EventArgs.Empty);
                });
                break;
            }
            case ChatReasoningDeltaEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var message = GetOrCreateAssistantReasoningMessage(x.ReasoningId);
                    message.IsStreaming = true;
                    message.Content += x.Content;
                    ContentAdded?.Invoke(this, EventArgs.Empty);
                });
                break;
            }
            case ChatReasoningEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var message = GetOrCreateAssistantReasoningMessage(x.ReasoningId);
                    message.Content = x.Content;
                    message.IsStreaming = false;
                    ContentAdded?.Invoke(this, EventArgs.Empty);
                });
                break;
            }
            case ChatToolExecutionStartEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    //AddMessage(new ChatMessageToolViewModel(x.Tool));
                });
                break;
            }
            case ChatUserMessageEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    //AddMessage(new ChatMessageUserViewModel(x.Content));
                });
                break;
            }
            case ChatButtonEvent x:
            {
                Dispatcher.UIThread.Post(() => { AddMessage(new ChatMessageWithButtonViewModel(x)); });
                break;
            }
            case ChatPermissionRequestEvent x:
            {
                Dispatcher.UIThread.Post(() => { AddMessage(new ChatMessagePermissionRequestViewModel(x)); });
                break;
            }
            case ChatErrorEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var errorMessage = string.IsNullOrWhiteSpace(x.Message)
                        ? "An unexpected error occurred."
                        : x.Message;

                    AddMessage(new ChatMessageAssistantViewModel()
                    {
                        Content = $"**Error:** {errorMessage}"
                    });
                    PersistActiveSession();
                });
                break;
            }
            case ChatIdleEvent:
            {
                FinishTurn();
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
            Messages.Clear();
            _assistantMessagesById.Clear();
            _assistantReasoningById.Clear();

            if (SelectedChatService != null)
            {
                if (_activeHistorySession == null || _activeHistorySession.ChatServiceName != SelectedChatService.Name)
                {
                    CreateNewActiveSession(SelectedChatService.Name);
                }

                _activeHistorySession!.Messages = [];
                _activeHistorySession.ChatId = SelectedChatService.ChatId;
                _activeHistorySession.UpdatedUtc = DateTime.UtcNow;
                PersistActiveSession();
            }
        });
    }

    public void RegisterChatService(IChatService chatService)
    {
        ChatServices.Add(chatService);
    }

    private void OnFunctionStarted(object? sender, AiFunctionStartedEvent function)
    {
        var newMessage = new ChatMessageToolViewModel(function.Id, function.FunctionName)
        {
            IsToolRunning = true,
            ToolOutput = $"{function.Detail}"
        };
        AddMessage(newMessage);
        ContentAdded?.Invoke(this, EventArgs.Empty);
    }

    private void OnFunctionPermissionRequested(object? sender, AiFunctionPermissionRequestEvent request)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var allowCommand = new RelayCommand<Control?>(_ =>
                request.DecisionSource.TrySetResult(AiFunctionPermissionDecision.AllowOnce));
            var denyCommand = new RelayCommand<Control?>(_ =>
                request.DecisionSource.TrySetResult(AiFunctionPermissionDecision.Deny));
            var allowForSessionCommand = new RelayCommand<Control?>(_ =>
                request.DecisionSource.TrySetResult(AiFunctionPermissionDecision.AllowForSession));

            var message = request.Question;
            if (!string.IsNullOrWhiteSpace(request.Detail))
                message = $"{request.Question}\n\n{request.Detail}";

            AddMessage(new ChatMessagePermissionRequestViewModel(new ChatPermissionRequestEvent(
                message,
                "Allow",
                "Deny",
                allowCommand,
                denyCommand,
                "Allow for session",
                allowForSessionCommand)));

            ContentAdded?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OnFunctionCompleted(object? sender, AiFunctionCompletedEvent function)
    {
        var toolFinished = Messages.OfType<ChatMessageToolViewModel>().LastOrDefault(x => x.Id == function.Id);
        if (toolFinished == null) return;

        toolFinished.IsToolRunning = false;
        toolFinished.IsSuccessful = function.Result;
        if (!string.IsNullOrWhiteSpace(function.ToolOutput))
        {
            if (string.IsNullOrWhiteSpace(toolFinished.ToolOutput))
                toolFinished.ToolOutput += '\n';
            toolFinished.ToolOutput += function.ToolOutput;
        }

        PersistActiveSession();
    }

    private void ShowEdit(AiEditViewModel? editViewModel)
    {
        if (editViewModel == null) return;

        _mainDockService.Show(editViewModel, DockShowLocation.Document);
    }

    public void SaveState()
    {
        PersistActiveSession();

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
        try
        {
            LoadHistoryEntries();

            ChatState? state = null;
            if (File.Exists(_statePath))
            {
                using var stream = File.OpenRead(_statePath);
                state = JsonSerializer.Deserialize<ChatState>(stream, ChatStateSerializerOptions);
            }

            if (state?.MessagesByService.Count > 0)
            {
                MigrateLegacyState(state);
                LoadHistoryEntries();
            }

            _activeHistorySessionId = state?.ActiveHistorySessionId;
            if (!string.IsNullOrWhiteSpace(_activeHistorySessionId) &&
                TryReadHistorySession(_activeHistorySessionId, out var activeSession))
            {
                _activeHistorySession = activeSession;
            }

            SelectedChatService = ChatServices.FirstOrDefault(x => x.Name == _activeHistorySession?.ChatServiceName) ??
                                  ChatServices.FirstOrDefault(x => x.Name == state?.SelectedChatServiceName) ??
                                  ChatServices.FirstOrDefault();

            if (SelectedChatService != null &&
                _activeHistorySession != null &&
                _activeHistorySession.ChatServiceName == SelectedChatService.Name)
            {
                LoadMessagesFromStates(_activeHistorySession.Messages);
                _ = RestoreServiceSessionIfNeededAsync(SelectedChatService, _activeHistorySession.ChatId);
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<Microsoft.Extensions.Logging.ILogger>()
                ?.Warning("Loading chat state failed", e);
        }
    }

    private ChatState BuildChatState()
    {
        return new ChatState
        {
            SelectedChatServiceName = SelectedChatService?.Name,
            ActiveHistorySessionId = _activeHistorySessionId
        };
    }

    private async Task LoadHistoryAsync(ChatHistoryItem? item)
    {
        if (item == null) return;
        if (!TryReadHistorySession(item.SessionId, out var session)) return;

        PersistActiveSession();

        _isLoadingExplicitHistory = true;
        try
        {
            _activeHistorySession = session;
            _activeHistorySessionId = session.SessionId;

            var targetService = ChatServices.FirstOrDefault(x => x.Name == session.ChatServiceName) ??
                                ChatServices.FirstOrDefault();
            if (targetService == null) return;

            SelectedChatService = targetService;
            LoadMessagesFromStates(session.Messages);

            await RestoreServiceSessionIfNeededAsync(targetService, session.ChatId);
            PersistActiveSession();
        }
        finally
        {
            _isLoadingExplicitHistory = false;
        }
    }

    private async Task RestoreServiceSessionIfNeededAsync(IChatService service, string? chatId)
    {
        if (string.IsNullOrWhiteSpace(chatId)) return;

        if (!IsInitialized)
        {
            await InitializeCurrentAsync();
        }

        try
        {
            await service.RestoreChatAsync(chatId);
            CaptureCurrentChatId();
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<Microsoft.Extensions.Logging.ILogger>()
                ?.Warning("Restoring chat session failed", e);
        }
    }

    private void EnsureActiveSessionForService(string serviceName)
    {
        if (_activeHistorySession != null && _activeHistorySession.ChatServiceName == serviceName)
        {
            LoadMessagesFromStates(_activeHistorySession.Messages);
            return;
        }

        var lastSession = HistoryItems
            .Where(x => x.ChatServiceName == serviceName)
            .OrderByDescending(x => x.UpdatedUtc)
            .FirstOrDefault();

        if (lastSession != null && TryReadHistorySession(lastSession.SessionId, out var historySession))
        {
            _activeHistorySession = historySession;
            _activeHistorySessionId = historySession.SessionId;
            LoadMessagesFromStates(historySession.Messages);
            _ = RestoreServiceSessionIfNeededAsync(SelectedChatService!, historySession.ChatId);
            return;
        }

        CreateNewActiveSession(serviceName);
        LoadMessagesFromStates(_activeHistorySession!.Messages);
    }

    private void CreateNewActiveSession(string serviceName)
    {
        _activeHistorySession = new ChatHistorySessionState
        {
            SessionId = Guid.NewGuid().ToString("N"),
            ChatServiceName = serviceName,
            ChatId = SelectedChatService?.ChatId,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            Title = $"{serviceName} chat",
            Messages = []
        };

        _activeHistorySessionId = _activeHistorySession.SessionId;
    }

    private void CaptureCurrentChatId()
    {
        if (SelectedChatService == null || _activeHistorySession == null) return;
        if (_activeHistorySession.ChatServiceName != SelectedChatService.Name) return;
        if (string.IsNullOrWhiteSpace(SelectedChatService.ChatId)) return;

        _activeHistorySession.ChatId = SelectedChatService.ChatId;
    }

    private void PersistActiveSession()
    {
        if (_activeHistorySession == null)
        {
            if (SelectedChatService == null) return;
            CreateNewActiveSession(SelectedChatService.Name);
        }

        _activeHistorySession!.ChatServiceName ??= SelectedChatService?.Name;
        CaptureCurrentChatId();

        _activeHistorySession.Messages = BuildCurrentMessageStates();
        _activeHistorySession.UpdatedUtc = DateTime.UtcNow;
        _activeHistorySession.Title = BuildSessionTitle(_activeHistorySession);

        try
        {
            WriteHistorySession(_activeHistorySession);
            UpsertHistoryItemSafe(_activeHistorySession);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<Microsoft.Extensions.Logging.ILogger>()
                ?.Warning("Saving history session failed", e);
        }
    }

    private string BuildSessionTitle(ChatHistorySessionState session)
    {
        var firstUser = session.Messages
            .FirstOrDefault(x => x.Kind == ChatMessageKind.User && !string.IsNullOrWhiteSpace(x.Message))
            ?.Message?.Trim();

        if (!string.IsNullOrWhiteSpace(firstUser))
        {
            const int maxLen = 72;
            return firstUser.Length > maxLen ? firstUser[..maxLen] + "..." : firstUser;
        }

        return $"{session.ChatServiceName ?? "Chat"} {session.CreatedUtc.ToLocalTime():g}";
    }

    private void WriteHistorySession(ChatHistorySessionState session)
    {
        if (!Directory.Exists(_historyDirectoryPath))
            Directory.CreateDirectory(_historyDirectoryPath);

        var path = GetHistorySessionPath(session.SessionId);
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, session, ChatStateSerializerOptions);
    }

    private bool TryReadHistorySession(string sessionId, out ChatHistorySessionState session)
    {
        var path = GetHistorySessionPath(sessionId);
        if (!File.Exists(path))
        {
            session = null!;
            return false;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var deserialized = JsonSerializer.Deserialize<ChatHistorySessionState>(stream, ChatStateSerializerOptions);
            if (deserialized == null)
            {
                session = null!;
                return false;
            }

            deserialized.SessionId = sessionId;
            deserialized.Messages ??= [];
            deserialized.CreatedUtc = deserialized.CreatedUtc == default ? DateTime.UtcNow : deserialized.CreatedUtc;
            deserialized.UpdatedUtc = deserialized.UpdatedUtc == default ? deserialized.CreatedUtc : deserialized.UpdatedUtc;

            session = deserialized;
            return true;
        }
        catch
        {
            session = null!;
            return false;
        }
    }

    private void LoadHistoryEntries()
    {
        HistoryItems.Clear();

        if (!Directory.Exists(_historyDirectoryPath))
            return;

        var sessions = new List<ChatHistorySessionState>();
        foreach (var file in Directory.EnumerateFiles(_historyDirectoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            var sessionId = Path.GetFileNameWithoutExtension(file);
            if (!TryReadHistorySession(sessionId, out var session))
                continue;

            sessions.Add(session);
        }

        foreach (var session in sessions.OrderByDescending(x => x.UpdatedUtc))
        {
            HistoryItems.Add(ChatHistoryItem.FromSession(session));
        }
    }

    private void UpsertHistoryItem(ChatHistorySessionState session)
    {
        var existing = HistoryItems.FirstOrDefault(x => x.SessionId == session.SessionId);
        if (existing == null)
        {
            HistoryItems.Add(ChatHistoryItem.FromSession(session));
        }
        else
        {
            existing.Title = session.Title ?? string.Empty;
            existing.ChatServiceName = session.ChatServiceName ?? string.Empty;
            existing.UpdatedUtc = session.UpdatedUtc;
            existing.UpdatedText = session.UpdatedUtc.ToLocalTime().ToString("g");
        }

        SortHistoryItems();
    }

    private void UpsertHistoryItemSafe(ChatHistorySessionState session)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            UpsertHistoryItem(session);
        }
        else
        {
            Dispatcher.UIThread.Post(() => UpsertHistoryItem(session));
        }
    }

    private void SortHistoryItems()
    {
        var ordered = HistoryItems.OrderByDescending(x => x.UpdatedUtc).ToList();
        HistoryItems.Clear();
        foreach (var item in ordered)
            HistoryItems.Add(item);
    }

    private string GetHistorySessionPath(string sessionId)
    {
        return Path.Combine(_historyDirectoryPath, $"{sessionId}.json");
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

    private void LoadMessagesFromStates(IReadOnlyCollection<ChatMessageState> states)
    {
        Messages.Clear();
        _assistantMessagesById.Clear();
        _assistantReasoningById.Clear();

        foreach (var messageState in states)
        {
            if (TryCreateMessage(messageState, out var message))
                Messages.Add(message);
        }
    }

    private void MigrateLegacyState(ChatState state)
    {
        if (state.MessagesByService.Count == 0) return;

        var selectedServiceName = state.SelectedChatServiceName;
        if (string.IsNullOrWhiteSpace(selectedServiceName) ||
            !state.MessagesByService.TryGetValue(selectedServiceName, out var messages))
        {
            var first = state.MessagesByService.First();
            selectedServiceName = first.Key;
            messages = first.Value;
        }

        var migrated = new ChatHistorySessionState
        {
            SessionId = Guid.NewGuid().ToString("N"),
            ChatServiceName = selectedServiceName,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            Title = "Migrated Chat",
            Messages = messages
        };

        WriteHistorySession(migrated);
        _activeHistorySessionId = migrated.SessionId;
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
                    Content = assistant.Content
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
                    IsStreaming = false
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
            default:
                message = null!;
                return false;
        }
    }

    private sealed class ChatState
    {
        public string? SelectedChatServiceName { get; set; }

        public string? ActiveHistorySessionId { get; set; }

        public Dictionary<string, List<ChatMessageState>> MessagesByService { get; set; } = new(StringComparer.Ordinal);
    }

    public sealed class ChatHistoryItem
    {
        public required string SessionId { get; set; }

        public required string Title { get; set; }

        public required string ChatServiceName { get; set; }

        public required DateTime UpdatedUtc { get; set; }

        public required string UpdatedText { get; set; }

        public static ChatHistoryItem FromSession(ChatHistorySessionState session)
        {
            return new ChatHistoryItem
            {
                SessionId = session.SessionId,
                Title = session.Title ?? string.Empty,
                ChatServiceName = session.ChatServiceName ?? string.Empty,
                UpdatedUtc = session.UpdatedUtc,
                UpdatedText = session.UpdatedUtc.ToLocalTime().ToString("g")
            };
        }
    }

    public sealed class ChatHistorySessionState
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N");

        public string? ChatServiceName { get; set; }

        public string? ChatId { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime UpdatedUtc { get; set; }

        public string? Title { get; set; }

        public List<ChatMessageState> Messages { get; set; } = [];
    }

    public sealed class ChatMessageState
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
        public bool IsSuccessful { get; set; }
    }

    public enum ChatMessageKind
    {
        User,
        Assistant,
        Reasoning,
        Tool
    }
}
