using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.ChatBot.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.ChatBot.ViewModels;

public partial class ChatBotViewModel : ExtendedTool
{
    public const string IconKey = "MaterialDesign.ChatBubbleOutline";

    private const string AssistantName = "Copilot";

    [ObservableProperty] private string _currentMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _statusText = "Starting Copilot...";
    [ObservableProperty] private string _selectedModel = "gpt-5";

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = new();
    public ObservableCollection<string> Models { get; } = new()
    {
        "gpt-4.1",
        "claude-sonnet-4.5"
    };

    private readonly IChatService _chatService;
    private ChatMessageViewModel? _activeAssistantMessage;
    private bool _initialized;

    private readonly IImage _assistantIcon;

    private readonly IImage _userIcon;
     
    public ChatBotViewModel(IChatService chatService) : base(IconKey)
    {
        Id = "ChatBot";
        Title = "OneAI Chat";
        
        using var stream = AssetLoader.Open(new Uri($"avares://OneWare.ChatBot/Assets/OneAi_Happy.png"));
        _assistantIcon = new Bitmap(stream);
        
        _userIcon = Application.Current!.FindResource(ThemeVariant.Light,  "Cool.User") as IImage ?? throw new Exception();

        _chatService = chatService;
        _chatService.MessageReceived += OnMessageReceived;
        _chatService.StatusChanged += OnStatusChanged;
    }

    public override void InitializeContent()
    {
        if (_initialized) return;
        _initialized = true;
        _ = InitializeChatAsync();
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var prompt = CurrentMessage.Trim();
        if (string.IsNullOrWhiteSpace(prompt)) return;

        if (!IsConnected)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Messages.Add(new ChatMessageViewModel("System", _assistantIcon, false)
                {
                    Message = "Copilot is not connected yet."
                });
            });
            return;
        }

        var userMessage = new ChatMessageViewModel("You", _userIcon, true)
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
            await _chatService.SendAsync(prompt);
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
        try
        {
            await _chatService.AbortAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSend() => IsConnected && !IsBusy && !string.IsNullOrWhiteSpace(CurrentMessage);

    private bool CanAbort() => IsConnected && IsBusy;

    partial void OnCurrentMessageChanged(string value) => SendCommand.NotifyCanExecuteChanged();

    partial void OnIsBusyChanged(bool value)
    {
        SendCommand.NotifyCanExecuteChanged();
        AbortCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsConnectedChanged(bool value)
    {
        SendCommand.NotifyCanExecuteChanged();
        AbortCommand.NotifyCanExecuteChanged();
    }

    private async Task InitializeChatAsync()
    {
        await _chatService.InitializeAsync(SelectedModel);
    }

    private ChatMessageViewModel CreateAssistantMessage()
    {
        return new ChatMessageViewModel(AssistantName, _assistantIcon, false)
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
                ? "An unexpected Copilot error occurred."
                : message;

            if (_activeAssistantMessage == null)
            {
                Messages.Add(new ChatMessageViewModel(AssistantName, _assistantIcon, false)
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

    partial void OnSelectedModelChanged(string value)
    {
        if (!_initialized) return;
        _ = SwitchModelAsync(value);
    }

    private async Task SwitchModelAsync(string model)
    {
        if (string.IsNullOrWhiteSpace(model)) return;

        IsBusy = false;
        _activeAssistantMessage = null;
        await _chatService.InitializeAsync(model);
    }
}
