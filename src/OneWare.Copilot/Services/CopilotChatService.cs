using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using OneWare.Copilot.Models;
using OneWare.Copilot.Views;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Copilot.Services;

public sealed class CopilotChatService(
    ISettingsService settingsService,
    IAiFunctionProvider toolProvider,
    IPackageWindowService packageService,
    IPaths paths)
    : ObservableObject, IChatService
{
    private CopilotClient? _client;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private CopilotSession? _session;
    private IDisposable? _subscription;
    private bool _forceNewSession;

    public ObservableCollection<ModelModel> Models { get; } = [];

    public ModelModel? SelectedModel
    {
        get;
        set
        {
            var oldValue = field;
            if (SetProperty(ref field, value) && value != null)
            {
                settingsService.SetSettingValue(CopilotModule.CopilotSelectedModelSettingKey, value.Id);
                if (oldValue != null)
                {
                    // When a model is changed we reset the session and force a new one on next init
                    SessionReset?.Invoke(this, EventArgs.Empty);
                    _ = NewChatAsync();
                }
            }
        }
    }

    public string Name { get; } = "Copilot";

    public Control BottomUiExtension => new CopilotChatExtensionView()
    {
        DataContext = this
    };

    public event EventHandler? SessionReset;
    public event EventHandler<ChatEvent>? EventReceived;
    public event EventHandler<StatusEvent>? StatusChanged;

    private async Task<bool> InstallCopilotCLiAsync()
    {
        var cliPath = settingsService.GetSettingValue<string>(CopilotModule.CopilotCliSettingKey);
        if (PlatformHelper.ExistsOnPath(cliPath)) return true;
        
        var autoDownload = settingsService.GetSettingValue<bool>("Experimental_AutoDownloadBinaries");

        if (!autoDownload) return false;
        
        var installResult = await packageService.QuickInstallPackageAsync(CopilotModule.CopilotPackage.Id!);

        if (!installResult) return false;
        
        SessionReset?.Invoke(this, EventArgs.Empty);
        
        var initResult = await InitializeAsync();
        
        return installResult;
    }
    
    private async Task<bool> AuthenticateAsync(Control? owner)
    {
        if (_client == null) return false;

        var cliPath = settingsService.GetSettingValue<string>(CopilotModule.CopilotCliSettingKey);

        
        
        return (await _client.GetAuthStatusAsync()).IsAuthenticated;
    }

    public async Task<bool> InitializeAsync()
    {
        var cliPath = settingsService.GetSettingValue<string>(CopilotModule.CopilotCliSettingKey);

        await _sync.WaitAsync().ConfigureAwait(false);
        await DisposeAsync();
        
        try
        {
            if (!PlatformHelper.ExistsOnPath(cliPath))
            {
                StatusChanged?.Invoke(this, new StatusEvent(false, "CLI Not found"));
                EventReceived?.Invoke(this, new ChatButtonEvent(
                    "Copilot CLI not found.", "Install Copilot CLI", new AsyncRelayCommand<Control?>(x => InstallCopilotCLiAsync())));
                return false;
            }

            _client = new CopilotClient(new CopilotClientOptions()
            {
                Cwd = paths.ProjectsDirectory,
                CliPath = cliPath
            });

            var authStatus = await _client.GetAuthStatusAsync();
            
            if (!authStatus.IsAuthenticated)
            {
                StatusChanged?.Invoke(this, new StatusEvent(false, "Not Authenticated"));
                EventReceived?.Invoke(this, new ChatButtonEvent(
                    "Not Authenticated to Copilot CLI.", "Login with GitHub", new AsyncRelayCommand<Control?>(AuthenticateAsync)));
                return false;
            }

            StatusChanged?.Invoke(this, new StatusEvent(false, $"Starting Copilot..."));

            await _client.StartAsync();

            var models = await _client.ListModelsAsync();

            StatusChanged?.Invoke(this, new StatusEvent(true, $"Copilot started"));

            Models.Clear();
            Models.AddRange(models.Select(x => new ModelModel()
            {
                Id = x.Id,
                Name = x.Name,
                Billing = $"{x.Billing?.Multiplier}x",
            }).ToArray());

            var selectedModelSetting =
                settingsService.GetSettingValue<string>(CopilotModule.CopilotSelectedModelSettingKey);
            SelectedModel = Models.FirstOrDefault(x => x.Id == selectedModelSetting) ??
                            Models.FirstOrDefault(x => x.Billing == "0x") ?? Models.FirstOrDefault();

            return true;
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, new StatusEvent(false, "Copilot unavailable"));
            EventReceived?.Invoke(this, new ChatErrorEvent(ex.Message));

            return false;
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task InitializeSessionAsync()
    {
        if (_client == null) return;

        await DisposeSessionAsync();

        if (SelectedModel == null)
        {
            EventReceived?.Invoke(this,
                new ChatErrorEvent("No Model Selected"));
            return;
        }

        StatusChanged?.Invoke(this, new StatusEvent(true, $"Connecting to {SelectedModel.Name}..."));

        string? sessionId = null;
        if (!_forceNewSession)
        {
            sessionId = await _client.GetLastSessionIdAsync();
        }

        if (sessionId == null)
        {
            _session = await _client.CreateSessionAsync(new SessionConfig
            {
                Model = SelectedModel.Id,
                Streaming = true,
                SystemMessage = new SystemMessageConfig
                {
                    Content = CopilotModule.SystemMessage
                },
                Tools = toolProvider.GetTools()
            });

            _forceNewSession = false;
        }
        else
        {
            _session = await _client.ResumeSessionAsync(sessionId, new ResumeSessionConfig()
            {
                Streaming = true,
                Tools = toolProvider.GetTools()
            });
        }
        
        StatusChanged?.Invoke(this, new StatusEvent(true, $"Connected"));
        
        _subscription = _session.On(HandleSessionEvent);
    }

    public async Task SendAsync(string prompt)
    {
        if (SelectedModel == null)
        {
            EventReceived?.Invoke(this,
                new ChatErrorEvent("No Model Selected"));
            return;
        }

        if (_session == null)
        {
            await InitializeSessionAsync();
        }

        if (_session == null) return;
        await _session.SendAsync(new MessageOptions { Prompt = prompt }).ConfigureAwait(false);
    }

    public async Task AbortAsync()
    {
        if (_session == null) return;
        await _session.AbortAsync();
    }

    public async Task NewChatAsync()
    {
        _forceNewSession = true;
        await InitializeSessionAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeSessionAsync();
        if (_client != null)
        {
            await _client.StopAsync();
            await _client.DisposeAsync();
        }
    }

    private async Task DisposeSessionAsync()
    {
        _subscription?.Dispose();
        _subscription = null;

        if (_session != null)
        {
            await _session.DisposeAsync();
            _session = null;
        }
    }

    private void HandleSessionEvent(SessionEvent evt)
    {
        switch (evt)
        {
            case AssistantMessageDeltaEvent x:
            {
                EventReceived?.Invoke(this,
                    new ChatMessageDeltaEvent(x.Data.DeltaContent, x.Data.MessageId));
                break;
            }
            case AssistantMessageEvent x:
            {
                EventReceived?.Invoke(this,
                    new ChatMessageEvent(x.Data.Content, x.Data.MessageId));
                break;
            }
            case AssistantReasoningDeltaEvent x:
            {
                EventReceived?.Invoke(this,
                    new ChatReasoningDeltaEvent(x.Data.DeltaContent, x.Data.ReasoningId));
                break;
            }
            case AssistantReasoningEvent x:
            {
                EventReceived?.Invoke(this,
                    new ChatReasoningEvent(x.Data.Content, x.Data.ReasoningId));
                break;
            }
            case UserMessageEvent x:
            {
                EventReceived?.Invoke(this,
                    new ChatUserMessageEvent(x.Data.Content));
                break;
            }
            case ToolExecutionStartEvent x:
            {
                EventReceived?.Invoke(this,
                    new ChatToolExecutionStartEvent(x.Data.ToolName));
                break;
            }
            case SessionErrorEvent error:
                EventReceived?.Invoke(this,
                    new ChatErrorEvent(error.Data.Message));
                break;
            case SessionIdleEvent:
                EventReceived?.Invoke(this, new ChatIdleEvent());
                break;
        }
    }
}