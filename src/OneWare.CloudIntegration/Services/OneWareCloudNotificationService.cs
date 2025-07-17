using System.Reactive.Disposables;
using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using Avalonia.Media;
using OneWare.Essentials.Services;
using Prism.Ioc;

namespace OneWare.CloudIntegration.Services;

public class OneWareCloudNotificationService
{
    private readonly OneWareCloudLoginService _loginService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;
    
    private HubConnection? _connection;
    private readonly List<HubSubscription> _subscriptions = [];
    
    public HubConnectionState ConnectionState => _connection?.State ?? HubConnectionState.Disconnected;

    public event EventHandler<HubConnectionState>? ConnectionStateChanged;
    
    public OneWareCloudNotificationService(OneWareCloudLoginService loginService, ISettingsService settingsService, ILogger logger)
    {
        _loginService = loginService;
        _settingsService = settingsService;
        _logger = logger;

        // Reset connection if host setting changes
        _settingsService.GetSettingObservable<string>(OneWareCloudIntegrationModule.OneWareCloudHostKey)
            .Subscribe(x =>
            {
                _ = DisconnectAsync();
            });
    }

    private async Task<string?> GetTokenAsync()
    {
        var (token, status) = await _loginService.GetLoggedInJwtTokenAsync();
        return token;
    }

    public async Task<bool> ConnectAsync()
    {
        if (_connection is { State: HubConnectionState.Connected })
            return true;

        await DisconnectAsync();
        
        var baseUrl = _settingsService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareCloudHostKey);

        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(baseUrl), "/hub"), options =>
            {
                options.AccessTokenProvider = GetTokenAsync;
            })
            .WithAutomaticReconnect()
            .Build();
        
        SubscribeEvents(_connection);
        
        foreach (var subscription in _subscriptions)
        {
            subscription.Attach(_connection);
        }
        
        try
        {
            await _connection.StartAsync();
            
            ConnectionStateChanged?.Invoke(this, HubConnectionState.Connected);

            string infoMsg = "Connected to OneWare Cloud";
            _logger.Log(infoMsg, ConsoleColor.Green);
            UserNotification.NewInformation(infoMsg)
                .ViaOutput(Brushes.Lime)
                .Send();
            
            return true;
        }
        catch (Exception e)
        {
            string warningMsg = "Failed to connect to OneWare Cloud";
            _logger.Warning(warningMsg, null);
            UserNotification.NewWarning(warningMsg)
                .ViaOutput()
                .Send();
            
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
    
    private Task On_ReconnectedAsync(string? e)
    {
        ConnectionStateChanged?.Invoke(this, HubConnectionState.Connected);
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Subscribes to a method from the SignalR hub and automatically parses the payload to the specified type.
    /// Returns a disposable to manually unsubscribe from reapplication logic.
    /// </summary>
    public IDisposable SubscribeToHubMethod<T>(string methodName, Action<T> handler)
    {
        var subscription = new HubSubscription
        {
            MethodName = methodName,
            RawHandler = handler,
            Attach = connection => connection.On(methodName, handler)
        };

        _subscriptions.Add(subscription);

        _connection?.On(methodName, handler); // attach immediately if live

        return new Subscription(() =>
        {
            _subscriptions.Remove(subscription);
        });
    }
    
    private class HubSubscription
    {
        public required string MethodName { get; init; } 
        public required Delegate RawHandler { get; init; }
        public required Action<HubConnection> Attach { get; init; }
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
}
