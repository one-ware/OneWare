using VtNetCore.Avalonia;

namespace OneWare.Terminal.Provider;

public class PseudoTerminalConnection(IPseudoTerminal terminal) : IConnection, IDisposable
{
    private CancellationTokenSource? _cancellationSource;

    public bool IsConnected { get; private set; }

    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    public event EventHandler<EventArgs>? Closed;

    public bool Connect()
    {
        _cancellationSource = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            var data = new byte[4096];

            while (!_cancellationSource.IsCancellationRequested)
            {
                var bytesReceived = await terminal.ReadAsync(data, 0, data.Length);

                if (bytesReceived > 0)
                {
                    var receivedData = new byte[bytesReceived];

                    Buffer.BlockCopy(data, 0, receivedData, 0, bytesReceived);

                    DataReceived?.Invoke(this, new DataReceivedEventArgs { Data = receivedData });
                }

                await Task.Delay(5);
            }
        }, _cancellationSource.Token);

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
        _ = terminal.WriteAsync(data, 0, data.Length);
    }

    public void SetTerminalWindowSize(int columns, int rows, int width, int height)
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

        Closed?.Invoke(this, EventArgs.Empty);
    }
}