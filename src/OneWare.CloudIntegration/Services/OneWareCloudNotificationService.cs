using System.IdentityModel.Tokens.Jwt;
using Avalonia.Media;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;

namespace OneWare.CloudIntegration.Services;

public class OneWareCloudNotificationService
{
    private readonly ILogger _logger;
    private readonly OneWareCloudLoginService _loginService;
    private readonly ISettingsService _settingsService;
    private readonly List<HubSubscription> _subscriptions = [];
    private readonly List<PersistentInvocation> _persistentInvocations = [];
    private readonly Lock _persistentInvocationsLock = new();

    private HubConnection? _connection;

    public OneWareCloudNotificationService(OneWareCloudLoginService loginService, ISettingsService settingsService,
        ILogger logger)
    {
        _loginService = loginService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public HubConnectionState ConnectionState => _connection?.State ?? HubConnectionState.Disconnected;

    public event EventHandler<HubConnectionState>? ConnectionStateChanged;

    private async Task<string?> GetTokenAsync()
    {
        var (token, status) = await _loginService.GetLoggedInJwtTokenAsync();
        return token?.RawData;
    }

    public async Task<bool> ConnectAsync()
    {
        if (_connection is { State: HubConnectionState.Connected })
            return true;

        await DisconnectAsync();

        var baseUrl = _settingsService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareCloudHostKey);

        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(baseUrl), "/hub"), options => { options.AccessTokenProvider = GetTokenAsync; })
            .WithAutomaticReconnect()
            .Build();

        SubscribeEvents(_connection);

        foreach (var subscription in _subscriptions) subscription.Attach(_connection);

        try
        {
            await _connection.StartAsync();

            ConnectionStateChanged?.Invoke(this, HubConnectionState.Connected);

            _logger.Log("Connected to OneWare Cloud", true, Brushes.Lime);

            return true;
        }
        catch (Exception e)
        {
            _logger.Warning("Failed to connect to OneWare Cloud", e);

            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_connection == null) return;

        try
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to disconnect SignalR: {ex.Message}");
        }

        UnsubscribeEvents(_connection);

        _connection = null;
        ConnectionStateChanged?.Invoke(this, HubConnectionState.Disconnected);
    }

    private void SubscribeEvents(HubConnection connection)
    {
        connection.Closed += On_ClosedAsync;
        connection.Reconnecting += On_ReconnectingAsync;
        connection.Reconnected += On_ReconnectedAsync;
    }

    private void UnsubscribeEvents(HubConnection connection)
    {
        connection.Closed -= On_ClosedAsync;
        connection.Reconnecting -= On_ReconnectingAsync;
        connection.Reconnected -= On_ReconnectedAsync;
    }

    private Task On_ClosedAsync(Exception? e)
    {
        ConnectionStateChanged?.Invoke(this, HubConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    private Task On_ReconnectingAsync(Exception? e)
    {
        ConnectionStateChanged?.Invoke(this, HubConnectionState.Reconnecting);
        return Task.CompletedTask;
    }

    private async Task On_ReconnectedAsync(string? e)
    {
        ConnectionStateChanged?.Invoke(this, HubConnectionState.Connected);

        // Replay any persistent invocations (e.g. group subscriptions on the hub) that
        // callers asked us to keep alive across reconnects.
        PersistentInvocation[] invocations;
        lock (_persistentInvocationsLock)
        {
            invocations = _persistentInvocations.ToArray();
        }

        foreach (var invocation in invocations)
        {
            try
            {
                if (_connection is { State: HubConnectionState.Connected })
                    await _connection.InvokeCoreAsync(invocation.MethodName, invocation.Args);
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to replay hub invocation '{invocation.MethodName}'", ex);
            }
        }
    }

    /// <summary>
    ///     Invokes a method on the SignalR hub. No-ops with a warning if the hub is not connected.
    /// </summary>
    public async Task InvokeHubMethodAsync(string methodName, params object?[] args)
    {
        if (_connection is not { State: HubConnectionState.Connected })
        {
            _logger.Warning($"Cannot invoke hub method '{methodName}': not connected.");
            return;
        }

        try
        {
            await _connection.InvokeCoreAsync(methodName, args);
        }
        catch (Exception ex)
        {
            _logger.Warning($"Failed to invoke hub method '{methodName}'", ex);
        }
    }

    /// <summary>
    ///     Registers a hub invocation that should be replayed automatically every time the
    ///     connection is (re)established. Typical use case: subscribing to server-side groups
    ///     such as a job progress stream. Disposing the returned handle stops the replay and
    ///     invokes the supplied <paramref name="unsubscribeMethodName"/> with the same args.
    /// </summary>
    /// <param name="subscribeMethodName">Hub method invoked now and on every reconnect.</param>
    /// <param name="unsubscribeMethodName">Hub method invoked on dispose. Pass <c>null</c> to skip.</param>
    /// <param name="args">Arguments forwarded to both methods.</param>
    public async Task<IAsyncDisposable> RegisterPersistentInvocationAsync(
        string subscribeMethodName,
        string? unsubscribeMethodName,
        params object?[] args)
    {
        var invocation = new PersistentInvocation
        {
            MethodName = subscribeMethodName,
            Args = args
        };

        lock (_persistentInvocationsLock)
        {
            _persistentInvocations.Add(invocation);
        }

        if (_connection is { State: HubConnectionState.Connected })
        {
            try
            {
                await _connection.InvokeCoreAsync(subscribeMethodName, args);
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to invoke hub method '{subscribeMethodName}'", ex);
            }
        }

        return new AsyncSubscription(async () =>
        {
            lock (_persistentInvocationsLock)
            {
                _persistentInvocations.Remove(invocation);
            }

            if (unsubscribeMethodName is null) return;

            if (_connection is { State: HubConnectionState.Connected })
            {
                try
                {
                    await _connection.InvokeCoreAsync(unsubscribeMethodName, args);
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Failed to invoke hub method '{unsubscribeMethodName}'", ex);
                }
            }
        });
    }

    /// <summary>
    ///     Subscribes to a method from the SignalR hub and automatically parses the payload to the specified type.
    ///     Returns a disposable to manually unsubscribe from reapplication logic.
    /// </summary>
    public IDisposable SubscribeToHubMethod<T>(string methodName, Action<T> handler)
    {
        IDisposable? hubUnsubscribe = null;

        var subscription = new HubSubscription
        {
            MethodName = methodName,
            RawHandler = handler,
            Attach = connection => hubUnsubscribe = connection.On(methodName, handler)
        };

        _subscriptions.Add(subscription);

        // Attach immediately if live and capture the unsubscriber
        if (_connection != null) hubUnsubscribe = _connection.On(methodName, handler);

        return new Subscription(() =>
        {
            _subscriptions.Remove(subscription);
            hubUnsubscribe?.Dispose();
        });
    }

    private class HubSubscription
    {
        public required string MethodName { get; init; }
        public required Delegate RawHandler { get; init; }
        public required Action<HubConnection> Attach { get; init; }
    }

    private class PersistentInvocation
    {
        public required string MethodName { get; init; }
        public required object?[] Args { get; init; }
    }

    private class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _isDisposed;

        public Subscription(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _unsubscribe();
            _isDisposed = true;
        }
    }

    private class AsyncSubscription : IAsyncDisposable
    {
        private readonly Func<Task> _unsubscribe;
        private bool _isDisposed;

        public AsyncSubscription(Func<Task> unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            await _unsubscribe();
        }
    }
}