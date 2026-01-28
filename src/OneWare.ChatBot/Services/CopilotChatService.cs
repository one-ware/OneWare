using System;
using System.Threading;
using System.Threading.Tasks;
using GitHub.Copilot.SDK;
using OneWare.ChatBot.Models;
using OneWare.Essentials.Services;

namespace OneWare.ChatBot.Services;

public sealed class CopilotChatService(
    IAiFunctionProvider toolProvider,
    IPaths paths)
    : IChatService
{
    private CopilotClient? _client;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private CopilotSession? _session;
    private string? _currentModel;
    private readonly IAiFunctionProvider _toolProvider = toolProvider;

    private IDisposable? _subscription;

    public string Name { get; } = "Copilot";

    public event EventHandler<ChatServiceMessageEvent>? MessageReceived;
    public event EventHandler<ChatServiceStatusEvent>? StatusChanged;

    public async Task<ModelModel[]> InitializeAsync()
    {
        await _sync.WaitAsync().ConfigureAwait(false);
        await DisposeAsync();

        try
        {
            _client = new CopilotClient(new CopilotClientOptions()
            {
                Cwd = paths.ProjectsDirectory,
            });

            StatusChanged?.Invoke(this, new ChatServiceStatusEvent(false, $"Starting Copilot..."));

            await _client.StartAsync();

            var models = await _client.ListModelsAsync();

            StatusChanged?.Invoke(this, new ChatServiceStatusEvent(true, $"Copilot started"));

            return models.Select(x => new ModelModel()
            {
                Id = x.Id,
                Name = x.Name,
                Billing = $"{x.Billing?.Multiplier}x",
            }).ToArray();
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, new ChatServiceStatusEvent(false, "Copilot unavailable"));
            MessageReceived?.Invoke(this, new ChatServiceMessageEvent(ChatServiceMessageType.Error, ex.Message));

            return [];
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task InitializeSessionAsync(string model)
    {
        if (_client == null) return;

        await DisposeSessionAsync();

        StatusChanged?.Invoke(this, new ChatServiceStatusEvent(true, $"Connecting to {model}..."));

        _session = await _client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            Streaming = true,
            SystemMessage = new SystemMessageConfig
            {
                Content = """
                          You are running inside an IDE called OneWare Studio. It supports opening multiple projects 

                          IMPORTANT RULES:
                          - THE CWD is not important since this App supports opening multiple projects in different locations. You can ask about the active project location with getActiveProject
                          - You DO NOT have access to files that are not open in the IDE (ask with getOpenFiles).
                          - You MUST NOT assume file contents, directory structure, or command output.
                          - You MUST use the provided tools to:
                            - discover open files
                            - determine the currently focused file
                            - execute terminal commands
                          - If a task requires file access or execution, you MUST call the appropriate tool.
                          - Never simulate terminal output.
                          - Never invent file paths or command results.
                          - If the user asks to edit something, start with the currently focused file (ask with getFocusedFile) (if not specified otherwise)
                          If a required tool is missing, ask the user.
                          """
            },
            Tools = _toolProvider.GetTools()
        });

        StatusChanged?.Invoke(this, new ChatServiceStatusEvent(true, $"Connected"));

        _currentModel = model;
        _subscription = _session.On(HandleSessionEvent);
    }

    public async Task SendAsync(string model, string prompt)
    {
        if (_session == null || _currentModel != model)
        {
            await InitializeSessionAsync(model);
        }

        if (_session == null) return;
        await _session.SendAsync(new MessageOptions { Prompt = prompt }).ConfigureAwait(false);
    }

    public async Task AbortAsync()
    {
        if (_session == null) return;
        await _session.AbortAsync().ConfigureAwait(false);
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
            await _session.DisposeAsync().ConfigureAwait(false);
            _session = null;
            _currentModel = null;
        }
    }

    private void HandleSessionEvent(SessionEvent evt)
    {
        switch (evt)
        {
            case AssistantMessageDeltaEvent delta:
                MessageReceived?.Invoke(this,
                    new ChatServiceMessageEvent(ChatServiceMessageType.AssistantDelta, delta.Data.DeltaContent));
                break;
            case AssistantMessageEvent message:
                MessageReceived?.Invoke(this,
                    new ChatServiceMessageEvent(ChatServiceMessageType.AssistantMessage, message.Data.Content));
                break;
            case SessionErrorEvent error:
                MessageReceived?.Invoke(this,
                    new ChatServiceMessageEvent(ChatServiceMessageType.Error, error.Data.Message));
                break;
            case SessionIdleEvent:
                MessageReceived?.Invoke(this, new ChatServiceMessageEvent(ChatServiceMessageType.Idle));
                break;
        }
    }
}
