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

    private readonly Dictionary<string, ChatMessageAssistantViewModel> _assistantMessagesById =
        new(StringComparer.Ordinal);

    private readonly Dictionary<string, ChatMessageReasoningViewModel> _assistantReasoningById =
        new(StringComparer.Ordinal);

    private readonly Dictionary<string, List<ChatMessageState>> _messagesByService =
        new(StringComparer.Ordinal);

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

    public IChatService? SelectedChatService
    {
        get;
        set
        {
            var oldValue = field;
            if (SetProperty(ref field, value))
            {
                if (oldValue != null)
                {
                    StoreCurrentMessages(oldValue.Name);
                }

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

                    LoadMessagesForService(value.Name);

                    InitializeCurrentCommand.Execute(null);
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

        return status;
    }

    private async Task NewChatAsync()
    {
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
                Dispatcher.UIThread.Post(() =>
                {
                    var errorMessage = string.IsNullOrWhiteSpace(x.Message)
                        ? "An unexpected error occurred."
                        : x.Message;

                    AddMessage(new ChatMessageAssistantViewModel()
                    {
                        Content = $"**Error:** {errorMessage}"
                    });
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
                _messagesByService[SelectedChatService.Name] = new List<ChatMessageState>();
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
            StoreCurrentMessages(SelectedChatService.Name);
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
        if (!File.Exists(_statePath)) return;

        try
        {
            using var stream = File.OpenRead(_statePath);
            var state = JsonSerializer.Deserialize<ChatState>(stream, ChatStateSerializerOptions);
            if (state == null) return;

            Messages.Clear();
            _assistantMessagesById.Clear();
            _assistantReasoningById.Clear();
            _messagesByService.Clear();

            foreach (var kvp in state.MessagesByService)
            {
                _messagesByService[kvp.Key] = kvp.Value;
            }

            SelectedChatService = ChatServices.FirstOrDefault(x => x.Name == state.SelectedChatServiceName) ??
                                  ChatServices.FirstOrDefault();
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
            MessagesByService = _messagesByService
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

        public Dictionary<string, List<ChatMessageState>> MessagesByService { get; set; } = new(StringComparer.Ordinal);
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

    private void StoreCurrentMessages(string serviceName)
    {
        var messages = new List<ChatMessageState>(Messages.Count);
        foreach (var message in Messages)
        {
            var state = BuildMessageState(message);
            if (state != null) messages.Add(state);
        }

        _messagesByService[serviceName] = messages;
    }

    private void LoadMessagesForService(string serviceName)
    {
        Messages.Clear();
        _assistantMessagesById.Clear();
        _assistantReasoningById.Clear();

        if (_messagesByService.TryGetValue(serviceName, out var states))
        {
            foreach (var messageState in states)
            {
                if (TryCreateMessage(messageState, out var message))
                    Messages.Add(message);
            }
        }
    }
}
