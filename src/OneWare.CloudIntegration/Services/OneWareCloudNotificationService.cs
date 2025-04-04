using Microsoft.AspNet.SignalR.Client;

namespace OneWare.CloudIntegration.Services;

public class OneWareCloudNotificationService
{
    private readonly OneWareCloudLoginService _loginService;
    private HubConnection? _connection;

    /// <summary>
    /// Gets the current state of the SignalR connection.
    /// </summary>
    public ConnectionState ConnectionState => _connection?.State ?? ConnectionState.Disconnected;

    /// <summary>
    /// Occurs when a notification is received from the hub.
    /// </summary>
    public event Action<string>? NotificationReceived;

    /// <summary>
    /// Occurs when the connection state changes.
    /// The first parameter is the previous state, and the second is the new state.
    /// </summary>
    public event Action<ConnectionState, ConnectionState>? ConnectionStateChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="OneWareCloudNotificationService"/> class.
    /// </summary>
    public OneWareCloudNotificationService(OneWareCloudLoginService loginService)
    {
        _loginService = loginService;
    }

    /// <summary>
    /// Connects to the SignalR hub.
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        // Avoid reconnecting if already connected.
        if (_connection is { State: ConnectionState.Connected })
            return true;

        var (token, status) = await _loginService.GetLoggedInJwtTokenAsync();

        if (token == null) return false;
        
        _connection = new HubConnection($"{OneWareCloudIntegrationModule.Host}/hub");
        {
            _connection.Headers.Add("Authorization", $"Bearer {token}");
        };

        // Handle connection state events.
        _connection.Reconnecting += () => 
        {
            ConnectionStateChanged?.Invoke(_connection.State, ConnectionState.Connecting);
            return;
        };
        
        _connection.Reconnected += () =>
        {
            ConnectionStateChanged?.Invoke(ConnectionState.Connecting, ConnectionState.Connected);
        };

        _connection.Closed += () =>
        {
            ConnectionStateChanged?.Invoke(_connection.State, ConnectionState.Disconnected);
        };

        // Subscribe to hub event "ReceiveNotification".
        // Adjust the event name and parameter types to match your hub's implementation.
        _connection.Received += data =>
        {
            NotificationReceived?.Invoke(data);
        };
        
        // Start the connection.
        await _connection.Start();
        ConnectionStateChanged?.Invoke(ConnectionState.Disconnected, _connection.State);

        return true;
    }

    /// <summary>
    /// Disconnects from the SignalR hub.
    /// </summary>
    public void Disconnect()
    {
        if (_connection != null)
        {
            _connection.Stop();
            _connection.Dispose();
            // Signal that the connection is now disconnected.
            ConnectionStateChanged?.Invoke(_connection.State, ConnectionState.Disconnected);
            _connection = null;
        }
    }
}
