using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using OneWare.Essentials.Services;
using Prism.Ioc;

namespace OneWare.CloudIntegration.Services;

public class OneWareCloudNotificationService
{
    private readonly OneWareCloudLoginService _loginService;
    private HubConnection _connection;

    public HubConnectionState ConnectionState => _connection?.State ?? HubConnectionState.Disconnected;

    public event Action<HubConnectionState, HubConnectionState>? ConnectionStateChanged;

    public OneWareCloudNotificationService(OneWareCloudLoginService loginService)
    {
        _loginService = loginService;
        
        _connection = new HubConnectionBuilder()
            .WithUrl($"{OneWareCloudIntegrationModule.Host}/hub", options =>
            {
                options.AccessTokenProvider = GetTokenAsync;
            })
            .WithAutomaticReconnect()
            .Build();
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

        _connection.Reconnecting += error =>
        {
            ConnectionStateChanged?.Invoke(HubConnectionState.Connected, HubConnectionState.Reconnecting);
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            ConnectionStateChanged?.Invoke(HubConnectionState.Reconnecting, HubConnectionState.Connected);
            return Task.CompletedTask;
        };

        _connection.Closed += error =>
        {
            ConnectionStateChanged?.Invoke(_connection.State, HubConnectionState.Disconnected);
            return Task.CompletedTask;
        };

        try
        {
            await _connection.StartAsync();
            ConnectionStateChanged?.Invoke(HubConnectionState.Disconnected, _connection.State);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start SignalR connection: {ex.Message}");
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        var previousState = _connection.State;
        await _connection.StopAsync();
        ConnectionStateChanged?.Invoke(previousState, HubConnectionState.Disconnected);
    }

    /// <summary>
    /// Subscribes to a method from the SignalR hub and automatically parses the payload to the specified type.
    /// </summary>
    public IDisposable SubscribeToHubMethod<T>(string methodName, Action<T> handler)
    {
        return _connection.On<T>(methodName, handler);
    }
}
