using VtNetCore.Avalonia;

namespace OneWare.Terminal.Provider;

public class PseudoTerminalConnection(IPseudoTerminal terminal) : IConnection, IDisposable
{
    private readonly ShellIntegrationParser _parser = new();
    private CancellationTokenSource? _cancellationSource;

    public bool IsConnected { get; private set; }

    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    public event EventHandler<EventArgs>? Closed;

    /// <summary>
    /// Raised, in stream order relative to <see cref="DataReceived"/>, for every
    /// shell-integration event (command started / command completed) emitted by the shell.
    /// The sequences themselves are stripped from the data before it is forwarded.
    /// </summary>
    public event EventHandler<ShellIntegrationEventArgs>? IntegrationEvent;

    /// <summary>
    /// Raised whenever data is written to the pty (user keystrokes or automation input),
    /// before the write happens. Allows consumers to correlate input with its echo.
    /// </summary>
    public event EventHandler<DataReceivedEventArgs>? DataSent;

    public bool Connect()
    {
        _cancellationSource = new CancellationTokenSource();

        _ = ReadOutputAsync(_cancellationSource.Token);

        IsConnected = true;

        terminal.Process.EnableRaisingEvents = true;
        terminal.Process.Exited += Process_Exited;

        return IsConnected;
    }

    public void Disconnect()
    {
        _cancellationSource?.Cancel();
        terminal.Dispose();
    }

    public void SendData(byte[] data)
    {
        DataSent?.Invoke(this, new DataReceivedEventArgs { Data = data });
        _ = terminal.WriteAsync(data, 0, data.Length);
    }

    public void KillProcess()
    {
        try
        {
            if (!terminal.Process.HasExited)
                terminal.Process.Kill(true);
        }
        catch
        {
            // Best effort: the process may already have exited or be unkillable.
        }
    }

    public void SetTerminalWindowSize(int columns, int rows)
    {
        terminal.SetSize(columns, rows);
    }

    public void Dispose()
    {
        _cancellationSource?.Cancel();

        Disconnect();
    }

    private void Process_Exited(object? sender, EventArgs e)
    {
        terminal.Process.Exited -= Process_Exited;
        _cancellationSource?.Cancel();

        Closed?.Invoke(this, EventArgs.Empty);
    }

    private async Task ReadOutputAsync(CancellationToken cancellationToken)
    {
        var data = new byte[4096];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesReceived = await terminal.ReadAsync(data, 0, data.Length);
                if (bytesReceived <= 0) break;

                var receivedData = new byte[bytesReceived];
                Buffer.BlockCopy(data, 0, receivedData, 0, bytesReceived);

                foreach (var segment in _parser.Feed(receivedData))
                {
                    if (segment.Data != null)
                        DataReceived?.Invoke(this, new DataReceivedEventArgs { Data = segment.Data });
                    else if (segment.Event is { } integrationEvent)
                        IntegrationEvent?.Invoke(this, new ShellIntegrationEventArgs(integrationEvent));
                }
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}

public sealed class ShellIntegrationEventArgs(ShellIntegrationEvent integrationEvent) : EventArgs
{
    public ShellIntegrationEvent Event { get; } = integrationEvent;

    public bool IsCommandStarted => Event.Command == 'C';

    public bool IsCommandCompleted => Event.Command == 'D';

    public int ExitCode => int.TryParse(Event.Argument, out var exitCode) ? exitCode : 0;
}
