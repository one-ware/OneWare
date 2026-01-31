using System.Collections.ObjectModel;
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
    private readonly Dictionary<string, ChatMessageAssistantViewModel> _assistantMessagesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ChatMessageReasoningViewModel> _assistantReasoningById = new(StringComparer.Ordinal);
    private bool _initialized;

    public ChatViewModel(IAiFunctionProvider aiFunctionProvider, IMainDockService mainDockService, AiFileEditService aiFileEditService) : base(IconKey)
    {
        Id = "AI_Chat";
        Title = "AI Chat";
        
        _mainDockService = mainDockService;
        aiFunctionProvider.FunctionStarted += OnFunctionStarted;
        aiFunctionProvider.FunctionCompleted += OnFunctionCompleted;
        AiFileEditService = aiFileEditService;
    }
    
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

    public bool NeedsAuthentication
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string StatusText
    {
        get;
        set => SetProperty(ref field, value);
    } = "Starting...";
    
    public ObservableCollection<IChatMessage> Messages { get; } = new();

    public ObservableCollection<IChatService> ChatServices { get; } = [];

    public IChatService? SelectedChatService
    {
        get;
        set
        {
            var oldValue = field;
            if (SetProperty(ref field, value))
            {
                NeedsAuthentication = false;

                if (oldValue != null)
                {
                    oldValue.EventReceived -= OnEventReceived;
                    oldValue.StatusChanged -= OnStatusChanged;
                }
                if (value != null)
                {
                    value.EventReceived += OnEventReceived;
                    value.StatusChanged += OnStatusChanged;
                    _ = InitializeChatAsync(value);
                }
            }
        }
    }
    
    public AiFileEditService AiFileEditService { get; }

    public RelayCommand<AiEditViewModel> ShowEditCommand => new(ShowEdit);

    public override void InitializeContent()
    {
        if (_initialized) return;
        _initialized = true;
        
        SelectedChatService = ChatServices.FirstOrDefault();
    }

    public AsyncRelayCommand InitializeCurrentCommand => new(() =>
    {
        if (SelectedChatService == null) return Task.CompletedTask;
        return InitializeChatAsync(SelectedChatService);
    });
    
    private async Task InitializeChatAsync(IChatService chatService)
    {
        Messages.Clear();
        _assistantMessagesById.Clear();
        
        var status = await chatService.InitializeAsync();

        IsInitialized = status.Success;
        NeedsAuthentication = status.NeedsAuthentication;
    }
    
    [RelayCommand]
    private async Task AuthenticateAsync()
    {
        if (SelectedChatService == null)
        {
            Messages.Add(new ChatMessageAssistantViewModel()
            {
                Content = "No ChatService Selected"
            });
            return;
        }
        NeedsAuthentication = !await SelectedChatService.AuthenticateAsync();
    }
    
    [RelayCommand]
    private async Task NewChatAsync()
    {
        if (SelectedChatService != null)
        {
            await AbortAsync();
            await SelectedChatService.NewChatAsync();
        }
        
        Messages.Clear();
        _assistantMessagesById.Clear();
    }
    
    [RelayCommand(CanExecute = nameof(CanSend))]
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

    [RelayCommand(CanExecute = nameof(CanAbort))]
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
                Dispatcher.UIThread.Post(() =>
                {
                    var message = GetOrCreateAssistantMessage(x.MessageId);
                    message.IsStreaming = true;
                    message.Content += x.Content;
                });
                break;
            }
            case ChatMessageEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var message = GetOrCreateAssistantMessage(x.MessageId);
                    message.Content = x.Content ?? string.Empty;
                    message.IsStreaming = false;
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
                });
                break;
            }
            case ChatReasoningEvent x:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var message = GetOrCreateAssistantReasoningMessage(x.ReasoningId);
                    message.Content = x.Content ?? string.Empty;
                    message.IsStreaming = false;
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

    public void RegisterChatService(IChatService chatService)
    {
        ChatServices.Add(chatService);
    }

    private void OnFunctionStarted(object? sender, string functionName)
    {
        var newMessage = new ChatMessageToolViewModel($"> {functionName}")
        {
            IsToolRunning = true
        };
        Messages.Add(newMessage);
    }
    
    private void OnFunctionCompleted(object? sender, string functionName)
    {
        var toolFinished = Messages.OfType<ChatMessageToolViewModel>().LastOrDefault(x => x.ToolMessage == $"> {functionName}");
        toolFinished?.IsToolRunning = false;
    }

    private void ShowEdit(AiEditViewModel? editViewModel)
    {
        if (editViewModel == null) return;
        
        _mainDockService.Show(editViewModel, DockShowLocation.Document);
    }
}
