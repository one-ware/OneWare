using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using Microsoft.Extensions.AI;
using OneWare.ChatBot.Services;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.ChatBot.ViewModels;

public partial class ChatBotViewModel : ExtendedTool, IChatManagerService
{
    public const string IconKey = "Bootstrap.ChatLeft";
    
    private readonly IMainDockService _mainDockService;
    private readonly Dictionary<string, ChatMessageViewModel> _assistantMessagesById = new(StringComparer.Ordinal);
    private ChatMessageViewModel? _activeAssistantMessage;
    private bool _initialized;

    public ChatBotViewModel(IAiFunctionProvider aiFunctionProvider, IMainDockService mainDockService, AiFileEditService aiFileEditService) : base(IconKey)
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
                OnCurrentMessageChanged(value);
        }
    } = string.Empty;

    public bool IsBusy
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                OnIsBusyChanged(value);
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
                OnIsConnectedChanged(value);
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
    
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = new();

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
                    oldValue.MessageReceived -= OnMessageReceived;
                    oldValue.StatusChanged -= OnStatusChanged;
                }
                if (value != null)
                {
                    value.MessageReceived += OnMessageReceived;
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
        _activeAssistantMessage = null;
        
        var status = await chatService.InitializeAsync();

        IsInitialized = status.Success;
        NeedsAuthentication = status.NeedsAuthentication;
    }
    
    [RelayCommand]
    private async Task AuthenticateAsync()
    {
        if (SelectedChatService == null)
        {
            Messages.Add(new ChatMessageViewModel("System", false)
            {
                Message = "No ChatService Selected"
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
        _activeAssistantMessage = null;
    }
    
    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var prompt = CurrentMessage.Trim();
        if (string.IsNullOrWhiteSpace(prompt)) return;

        if (SelectedChatService == null)
        {
            Messages.Add(new ChatMessageViewModel("System", false)
            {
                Message = "No ChatService Selected"
            });
            return;
        }
        
        if (!IsConnected)
        {
            Messages.Add(new ChatMessageViewModel("System", false)
            {
                Message = $"{SelectedChatService.Name} is not connected yet."
            });
            return;
        }

        var userMessage = new ChatMessageViewModel("You", true)
        {
            Message = prompt
        };
        var assistantMessage = CreateAssistantMessage();

        Messages.Add(userMessage);
        Messages.Add(assistantMessage);

        _activeAssistantMessage = assistantMessage;
        CurrentMessage = string.Empty;
        IsBusy = true;

        try
        {
            await SelectedChatService.SendAsync(prompt);
        }
        catch (Exception ex)
        {
            assistantMessage.IsStreaming = false;
            assistantMessage.Message = $"**Error:** {ex.Message}";
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

    private void OnCurrentMessageChanged(string value) => SendCommand.NotifyCanExecuteChanged();

    private void OnIsBusyChanged(bool value)
    {
        SendCommand.NotifyCanExecuteChanged();
        AbortCommand.NotifyCanExecuteChanged();
    }

    private void OnIsConnectedChanged(bool value)
    {
        SendCommand.NotifyCanExecuteChanged();
        AbortCommand.NotifyCanExecuteChanged();
    }

    private ChatMessageViewModel CreateAssistantMessage()
    {
        return new ChatMessageViewModel(SelectedChatService?.Name ?? "System", false)
        {
            IsStreaming = true
        };
    }

    private ChatMessageViewModel GetOrCreateAssistantMessage(string? messageId)
    {
        if (!string.IsNullOrWhiteSpace(messageId))
        {
            if (_assistantMessagesById.TryGetValue(messageId, out var existing))
                return existing;

            if (_activeAssistantMessage != null && string.IsNullOrWhiteSpace(_activeAssistantMessage.MessageId))
            {
                _activeAssistantMessage.MessageId = messageId;
                _assistantMessagesById[messageId] = _activeAssistantMessage;
                return _activeAssistantMessage;
            }

            var created = CreateAssistantMessage();
            created.MessageId = messageId;
            Messages.Add(created);
            _assistantMessagesById[messageId] = created;
            _activeAssistantMessage = created;
            return created;
        }

        if (_activeAssistantMessage == null)
        {
            _activeAssistantMessage = CreateAssistantMessage();
            Messages.Add(_activeAssistantMessage);
        }

        return _activeAssistantMessage;
    }

    private ChatMessageViewModel? FindAssistantMessage(string? messageId)
    {
        if (!string.IsNullOrWhiteSpace(messageId) && _assistantMessagesById.TryGetValue(messageId, out var byId))
            return byId;

        return _activeAssistantMessage;
    }

    private void AppendAssistantDelta(string? delta, string? messageId)
    {
        if (string.IsNullOrEmpty(delta)) return;
        Dispatcher.UIThread.Post(() =>
        {
            var message = GetOrCreateAssistantMessage(messageId);
            message.IsStreaming = true;
            message.Message += delta;
        });
    }

    private void FinalizeAssistantMessage(string? content, string? messageId)
    {
        if (content == null && string.IsNullOrWhiteSpace(messageId)) return;
        Dispatcher.UIThread.Post(() =>
        {
            var message = GetOrCreateAssistantMessage(messageId);
            if (content != null)
                message.Message = content;
            message.IsStreaming = false;
        });
    }

    private void HandleSessionError(string? message, string? messageId)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var errorMessage = string.IsNullOrWhiteSpace(message)
                ? "An unexpected error occurred."
                : message;

            var targetMessage = FindAssistantMessage(messageId);
            if (targetMessage == null)
            {
                Messages.Add(new ChatMessageViewModel(SelectedChatService?.Name ?? "System", false)
                {
                    Message = $"**Error:** {errorMessage}"
                });
            }
            else
            {
                targetMessage.IsStreaming = false;
                targetMessage.Message = $"**Error:** {errorMessage}";
                if (ReferenceEquals(targetMessage, _activeAssistantMessage))
                    _activeAssistantMessage = null;
            }

            IsBusy = false;
        });
    }

    private void FinishTurn()
    {
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var message in Messages)
                message.IsStreaming = false;

            _activeAssistantMessage = null;
            IsBusy = false;
        });
    }

    private void OnMessageReceived(object? sender, ChatServiceMessageEvent e)
    {
        switch (e.Type)
        {
            case ChatServiceMessageType.AssistantDelta:
                AppendAssistantDelta(e.Content, e.MessageId);
                break;
            case ChatServiceMessageType.AssistantMessage:
                FinalizeAssistantMessage(e.Content, e.MessageId);
                break;
            case ChatServiceMessageType.Error:
                HandleSessionError(e.Content, e.MessageId);
                break;
            case ChatServiceMessageType.Idle:
                FinishTurn();
                break;
        }
    }

    private void OnStatusChanged(object? sender, ChatServiceStatusEvent e)
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
        var lastMessageStreaming = Messages.LastOrDefault(x => x.IsStreaming);
 
        var newMessage = new ChatMessageViewModel(string.Empty, false)
        {
            Message = $"> {functionName}",
            IsToolMessage = true,
            IsToolRunning = true
        };
            
        if (lastMessageStreaming != null)
        {
            Messages.Insert(Messages.IndexOf(lastMessageStreaming), newMessage);
        }
        else Messages.Add(newMessage);
    }
    
    private void OnFunctionCompleted(object? sender, string functionName)
    {
        var toolFinished = Messages.LastOrDefault(x => x.IsToolMessage && x.Message == $"> {functionName}");
        toolFinished?.IsToolRunning = false;
    }

    public void ShowEdit(AiEditViewModel? editViewModel)
    {
        if (editViewModel == null) return;
        
        _mainDockService.Show(editViewModel, DockShowLocation.Document);
    }
}
