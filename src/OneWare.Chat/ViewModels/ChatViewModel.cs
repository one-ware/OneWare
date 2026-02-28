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

    private readonly IMainDockService _mainDockService;
    private readonly string _statePath;
    private readonly string _historyRootPath;

    private readonly Dictionary<string, ChatMessageAssistantViewModel> _assistantMessagesById =
        new(StringComparer.Ordinal);

    private readonly Dictionary<string, ChatMessageReasoningViewModel> _assistantReasoningById =
        new(StringComparer.Ordinal);
    
    private readonly Dictionary<string, string> _selectedSessionByService = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ChatSessionHistoryItem>> _historyByService = new(StringComparer.Ordinal);

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

        var chatDirectory = Path.Combine(paths.AppDataDirectory, "Chat");
        _statePath = Path.Combine(chatDirectory, "ChatState.json");
        _historyRootPath = Path.Combine(chatDirectory, "History");

        AiFileEditService = aiFileEditService;

        NewChatCommand = new AsyncRelayCommand(NewChatAsync);
        SendCommand = new AsyncRelayCommand(SendAsync, CanSend);
        AbortCommand = new AsyncRelayCommand(AbortAsync, CanAbort);
        InitializeCurrentCommand = new AsyncRelayCommand(InitializeCurrentAsync);

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
            }

            if (value == null) return;

            value.EventReceived += OnEventReceived;
            value.StatusChanged += OnStatusChanged;
            value.SessionReset += OnSessionReset;
            
            _ = InitializeAndRestoreCurrentServiceAsync(value);
        }
    }

    public AiFileEditService AiFileEditService { get; }

    public RelayCommand<AiEditViewModel> ShowEditCommand => new(ShowEdit);

    public AsyncRelayCommand NewChatCommand { get; }

    public AsyncRelayCommand SendCommand { get; }

    public AsyncRelayCommand AbortCommand { get; }

    public AsyncRelayCommand InitializeCurrentCommand { get; }

    public override void InitializeContent()
    {
        if (_initialized) return;
        _initialized = true;

        LoadState();
    }

    private async Task<bool> InitializeCurrentAsync()
    {
        if (SelectedChatService == null) return false;
        
        var status = await SelectedChatService.InitializeAsync();

        IsInitialized = status;

        return status;
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
        _assistantMessagesById.Clear();
        _assistantReasoningById.Clear();
    }

    private async Task SendAsync()
    {
        var prompt = CurrentMessage.Trim();
        if (string.IsNullOrWhiteSpace(prompt)) return;

        if (SelectedChatService == null)
        {
            AddErrorMessage("No chat service selected.");
            return;
        }

        if (!IsInitialized)
        {
            await InitializeCurrentAsync();
        }

        if (!IsConnected)
        {
            AddErrorMessage($"{SelectedChatService.Name} is not connected yet.");
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
            if (Messages.LastOrDefault() is ChatMessageAssistantViewModel { MessageId: "init" } initMessage)
            {
                Messages.Remove(initMessage);
            }
            else
            {
                Messages.Remove(assistantMessage);
            }

            AddErrorMessage(ex.Message);
            IsBusy = false;
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

    private void AddErrorMessage(string? message)
    {
        var errorMessage = string.IsNullOrWhiteSpace(message)
            ? "An unexpected error occurred."
            : message;

        AddMessage(new ChatMessageErrorViewModel(errorMessage));
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
            case ChatToolExecutionStartEvent:
            {
                break;
            }
            case ChatUserMessageEvent:
            {
                break;
            }
            case ChatButtonEvent x:
            {
                Dispatcher.UIThread.Post(() => { AddMessage(new ChatMessageWithButtonViewModel(x)); });
                break;
            }
            case ChatPermissionRequestEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var msg = new ChatMessagePermissionRequestViewModel(x);
                    msg.CloseAction = () => Messages.Remove(msg);
                    AddMessage(msg);
                });
                break;
            }
            case ChatErrorEvent x:
            {
                Dispatcher.UIThread.Post(() => { AddErrorMessage(x.Message); });
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
                UpdateSelectedSessionFromService(SelectedChatService);
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

            var msg = new ChatMessagePermissionRequestViewModel(new ChatPermissionRequestEvent(
                message,
                "Allow",
                "Deny",
                allowCommand,
                denyCommand,
                "Allow for session",
                allowForSessionCommand));

            msg.CloseAction = () => Messages.Remove(msg);
            AddMessage(msg);
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
    }

    private void ShowEdit(AiEditViewModel? editViewModel)
    {
        if (editViewModel == null) return;

        _mainDockService.Show(editViewModel, DockShowLocation.Document);
    }

    public void SaveState()
    {
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

                    SelectedChatService = ChatServices.FirstOrDefault(x => x.Name == state.SelectedChatServiceName) ??
                                          ChatServices.FirstOrDefault();
                    return;
                }
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<Microsoft.Extensions.Logging.ILogger>()
                    ?.Warning("Loading chat state failed", e);
            }
        }

        SelectedChatService = ChatServices.FirstOrDefault();
    }

    private ChatState BuildChatState()
    {
        return new ChatState
        {
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
            case ChatMessageErrorViewModel:
                // Error chat messages are intentionally not serialized.
                return null;
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
        _assistantReasoningById.Clear();
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

    private async Task LoadSessionAsync(ChatSessionHistoryItem? item)
    {
        if (item == null || SelectedChatService == null) return;
        if (!string.Equals(item.ServiceName, SelectedChatService.Name, StringComparison.Ordinal)) return;
        if (SelectedChatService is IChatServiceWithSessions s && s.CurrentSessionId == item.SessionId) return;
        
        StoreCurrentMessages(SelectedChatService.Name, SelectedChatService);

        if (SelectedChatService is IChatServiceWithSessions serviceWithSessions)
        {
            if (!IsInitialized)
            {
                await InitializeCurrentAsync();
            }

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
        
        ContentAdded?.Invoke(this, EventArgs.Empty);
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
        SessionHistory.Add(new ChatSessionHistoryItem()
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
        public bool IsSuccessful { get; set; }
    }

    private enum ChatMessageKind
    {
        User,
        Assistant,
        Reasoning,
        Tool
    }
}
