using Microsoft.AspNetCore.SignalR.Client;

namespace OneWare.CloudIntegration.Services;

public class OneWareCloudNotificationService
{
    private readonly OneWareCloudLoginService _loginService;
    private HubConnection? _connection;

    /// <summary>
    /// Gets the current state of the SignalR connection.
    /// </summary>
    public HubConnectionState ConnectionState => _connection?.State ?? HubConnectionState.Disconnected;

    /// <summary>
    /// Occurs when a notification is received from the hub.
    /// </summary>
    public event Action<string>? NotificationReceived;

    /// <summary>
    /// Occurs when the connection state changes.
    /// The first parameter is the previous state, and the second is the new state.
    /// </summary>
    public event Action<HubConnectionState, HubConnectionState>? ConnectionStateChanged;

    public OneWareCloudNotificationService(OneWareCloudLoginService loginService)
    {
        _loginService = loginService;
    }

    /// <summary>
    /// Connects to the SignalR hub.
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        if (_connection is { State: HubConnectionState.Connected })
            return true;

        var (token, status) = await _loginService.GetLoggedInJwtTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return false;

        _connection = new HubConnectionBuilder()
            .WithUrl($"{OneWareCloudIntegrationModule.Host}/hub", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token)!;
            })
            .WithAutomaticReconnect()
            .Build();

        // Hook up connection state events
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

        // Hook up notification event
        _connection.On<string>("ReceiveMessage", message =>
        {
            NotificationReceived?.Invoke(message);
        });

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

    /// <summary>
    /// Disconnects from the SignalR hub.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            var previousState = _connection.State;
            await _connection.StopAsync();
            await _connection.DisposeAsync();
            ConnectionStateChanged?.Invoke(previousState, HubConnectionState.Disconnected);
            _connection = null;
        }
    }
}
