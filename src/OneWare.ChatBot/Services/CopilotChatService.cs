using System;
using System.Threading;
using System.Threading.Tasks;
using GitHub.Copilot.SDK;

namespace OneWare.ChatBot.Services;

public sealed class CopilotChatService : IChatService
{
    private readonly CopilotClient _client;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private CopilotSession? _session;
    private IDisposable? _subscription;
    private bool _started;

    public CopilotChatService()
    {
        _client = new CopilotClient(new CopilotClientOptions
        {
            AutoStart = false,
            CliPath = "/home/hmenn/.homes/ubuntu-dev/.local/bin/copilot"
        });
    }

    public event EventHandler<ChatServiceMessageEvent>? MessageReceived;
    public event EventHandler<ChatServiceStatusEvent>? StatusChanged;

    public async Task InitializeAsync(string model)
    {
        await _sync.WaitAsync().ConfigureAwait(false);
        try
        {
            StatusChanged?.Invoke(this, new ChatServiceStatusEvent(false, $"Connecting to {model}..."));

            if (!_started)
            {
                await _client.StartAsync().ConfigureAwait(false);
                _started = true;
            }

            await DisposeSessionAsync().ConfigureAwait(false);

            _session = await _client.CreateSessionAsync(new SessionConfig
            {
                Model = model,
                Streaming = true
            }).ConfigureAwait(false);

            _subscription = _session.On(HandleSessionEvent);

            StatusChanged?.Invoke(this, new ChatServiceStatusEvent(true, $"Connected ({model})"));
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, new ChatServiceStatusEvent(false, "Copilot unavailable"));
            MessageReceived?.Invoke(this, new ChatServiceMessageEvent(ChatServiceMessageType.Error, ex.Message));
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task SendAsync(string prompt)
    {
        if (_session == null) throw new InvalidOperationException("Copilot session is not ready.");
        await _session.SendAsync(new MessageOptions { Prompt = prompt }).ConfigureAwait(false);
    }

    public async Task AbortAsync()
    {
        if (_session == null) return;
        await _session.AbortAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeSessionAsync().ConfigureAwait(false);
        if (_started)
            await _client.StopAsync().ConfigureAwait(false);
        await _client.DisposeAsync().ConfigureAwait(false);
        _sync.Dispose();
    }

    private async Task DisposeSessionAsync()
    {
        _subscription?.Dispose();
        _subscription = null;

        if (_session != null)
        {
            await _session.DisposeAsync().ConfigureAwait(false);
            _session = null;
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
