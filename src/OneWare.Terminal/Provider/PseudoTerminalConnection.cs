using System.Collections.Generic;
using VtNetCore.Avalonia;

namespace OneWare.Terminal.Provider;

public class PseudoTerminalConnection(IPseudoTerminal terminal) : IConnection, IOutputFilter, IOutputSuppressor, IDisposable
{
    private CancellationTokenSource? _cancellationSource;
    private readonly object _suppressLock = new();
    private readonly Queue<byte[]> _suppressQueue = new();
    private readonly List<byte> _pendingSuppression = new();
    private byte[]? _activeSuppression;

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

    public void SuppressOutput(byte[] sequence)
    {
        if (sequence.Length == 0) return;
        lock (_suppressLock)
        {
            _suppressQueue.Enqueue(sequence);
        }
    }

    public byte[] FilterOutput(byte[] data)
    {
        if (data.Length == 0) return data;

        lock (_suppressLock)
        {
            if (_activeSuppression == null && _suppressQueue.Count == 0) return data;

            var output = new List<byte>(data.Length + 8);
            var index = 0;

            while (index < data.Length)
            {
                if (_activeSuppression == null && _suppressQueue.Count > 0)
                {
                    _activeSuppression = _suppressQueue.Dequeue();
                    _pendingSuppression.Clear();
                }

                if (_activeSuppression == null)
                {
                    output.Add(data[index++]);
                    continue;
                }

                var b = data[index++];
                var pendingIndex = _pendingSuppression.Count;

                if (b == _activeSuppression[pendingIndex])
                {
                    _pendingSuppression.Add(b);
                    if (_pendingSuppression.Count == _activeSuppression.Length)
                    {
                        _activeSuppression = null;
                        _pendingSuppression.Clear();
                    }

                    continue;
                }

                if (_pendingSuppression.Count > 0)
                {
                    output.AddRange(_pendingSuppression);
                    _pendingSuppression.Clear();
                }

                if (_activeSuppression != null && b == _activeSuppression[0])
                {
                    _pendingSuppression.Add(b);
                }
                else
                {
                    output.Add(b);
                }
            }

            return output.Count == data.Length ? data : output.ToArray();
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

        Closed?.Invoke(this, EventArgs.Empty);
    }
}
