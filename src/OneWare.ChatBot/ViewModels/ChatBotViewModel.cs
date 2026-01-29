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
    public const string IconKey = "MaterialDesign.ChatBubbleOutline";
    
    private readonly IMainDockService _mainDockService;
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

    public bool IsConnected
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                OnIsConnectedChanged(value);
        }
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

    private async Task InitializeChatAsync(IChatService chatService)
    {
        await chatService.InitializeAsync();
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var prompt = CurrentMessage.Trim();
        if (string.IsNullOrWhiteSpace(prompt)) return;

        if (SelectedChatService == null)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Messages.Add(new ChatMessageViewModel("System", false)
                {
                    Message = "No ChatService Selected"
                });
            });
            return;
        }
        
        if (!IsConnected)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Messages.Add(new ChatMessageViewModel("System", false)
                {
                    Message = $"{SelectedChatService.Name} is not connected yet."
                });
            });
            return;
        }

        var userMessage = new ChatMessageViewModel("You", true)
        {
            Message = prompt
        };
        var assistantMessage = CreateAssistantMessage();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Messages.Add(userMessage);
            Messages.Add(assistantMessage);
        });

        _activeAssistantMessage = assistantMessage;
        CurrentMessage = string.Empty;
        IsBusy = true;

        try
        {
            await SelectedChatService.SendAsync(prompt);
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                assistantMessage.IsStreaming = false;
                assistantMessage.Message = $"**Error:** {ex.Message}";
                IsBusy = false;
            });
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

    private void AppendAssistantDelta(string? delta)
    {
        if (string.IsNullOrEmpty(delta)) return;
        Dispatcher.UIThread.Post(() =>
        {
            var message = _activeAssistantMessage;
            if (message == null)
            {
                message = CreateAssistantMessage();
                Messages.Add(message);
                _activeAssistantMessage = message;
            }

            message.Message += delta;
        });
    }

    private void FinalizeAssistantMessage(string? content)
    {
        if (string.IsNullOrEmpty(content)) return;
        Dispatcher.UIThread.Post(() =>
        {
            var message = _activeAssistantMessage;
            if (message == null)
            {
                message = CreateAssistantMessage();
                Messages.Add(message);
                _activeAssistantMessage = message;
            }

            message.Message = content;
            message.IsStreaming = false;
        });
    }

    private void HandleSessionError(string? message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var errorMessage = string.IsNullOrWhiteSpace(message)
                ? "An unexpected error occurred."
                : message;

            if (_activeAssistantMessage == null)
            {
                Messages.Add(new ChatMessageViewModel(SelectedChatService?.Name ?? "System", false)
                {
                    Message = $"**Error:** {errorMessage}"
                });
            }
            else
            {
                _activeAssistantMessage.IsStreaming = false;
                _activeAssistantMessage.Message = $"**Error:** {errorMessage}";
                _activeAssistantMessage = null;
            }

            IsBusy = false;
        });
    }

    private void FinishTurn()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_activeAssistantMessage != null)
                _activeAssistantMessage.IsStreaming = false;

            _activeAssistantMessage = null;
            IsBusy = false;
        });
    }

    private void OnMessageReceived(object? sender, ChatServiceMessageEvent e)
    {
        switch (e.Type)
        {
            case ChatServiceMessageType.AssistantDelta:
                AppendAssistantDelta(e.Content);
                break;
            case ChatServiceMessageType.AssistantMessage:
                FinalizeAssistantMessage(e.Content);
                break;
            case ChatServiceMessageType.Error:
                HandleSessionError(e.Content);
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
