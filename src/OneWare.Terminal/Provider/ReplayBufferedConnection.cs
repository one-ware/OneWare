using VtNetCore.Avalonia;

namespace OneWare.Terminal.Provider;

/// <summary>
/// Wraps a pty connection for the terminal control. The control only consumes
/// <see cref="DataReceived"/> while it is attached to the visual tree, so output produced
/// while the terminal is hidden (an inactive dock tab, a chat message scrolled out of a
/// virtualized list) would be lost. This wrapper keeps that output and replays it as soon as
/// a control subscribes again.
/// </summary>
/// <remarks>
/// The buffer is only filled while there is no subscriber, so data is never delivered twice.
/// </remarks>
public sealed class ReplayBufferedConnection : IConnection, IDisposable
{
    private const int MaxBufferedBytes = 1024 * 1024;

    private readonly IConnection _inner;
    private readonly Lock _lock = new();
    private readonly Queue<byte[]> _pending = new();

    private EventHandler<DataReceivedEventArgs>? _dataReceived;
    private int _pendingBytes;
    private bool _disposed;

    public ReplayBufferedConnection(IConnection inner)
    {
        _inner = inner;
        _inner.DataReceived += OnInnerDataReceived;
    }

    public bool IsConnected => _inner.IsConnected;

    public event EventHandler<DataReceivedEventArgs>? DataReceived
    {
        add
        {
            if (value == null) return;

            lock (_lock)
            {
                var wasEmpty = _dataReceived == null;
                _dataReceived += value;

                if (!wasEmpty) return;

                while (_pending.Count > 0)
                    value(this, new DataReceivedEventArgs { Data = _pending.Dequeue() });

                _pendingBytes = 0;
            }
        }
        remove
        {
            if (value == null) return;
            lock (_lock) _dataReceived -= value;
        }
    }

    public event EventHandler<EventArgs> Closed
    {
        add => _inner.Closed += value;
        remove => _inner.Closed -= value;
    }

    public bool Connect()
    {
        return _inner.Connect();
    }

    public void Disconnect()
    {
        _inner.Disconnect();
    }

    public void SendData(byte[] data)
    {
        _inner.SendData(data);
    }

    public void SetTerminalWindowSize(int columns, int rows)
    {
        _inner.SetTerminalWindowSize(columns, rows);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _pending.Clear();
            _pendingBytes = 0;
            _dataReceived = null;
        }

        _inner.DataReceived -= OnInnerDataReceived;
    }

    private void OnInnerDataReceived(object? sender, DataReceivedEventArgs e)
    {
        // Delivery and buffering happen under the same lock so that a control subscribing
        // concurrently cannot receive replayed and live data out of order. Subscribers only
        // queue the data for the UI thread, so holding the lock is cheap.
        lock (_lock)
        {
            if (_disposed) return;

            if (_dataReceived != null)
            {
                _dataReceived(this, e);
                return;
            }

            _pending.Enqueue(e.Data);
            _pendingBytes += e.Data.Length;

            // Drop the oldest output when a hidden terminal produces more than the buffer
            // holds; that part has scrolled out of the terminal's own scrollback anyway.
            while (_pendingBytes > MaxBufferedBytes && _pending.Count > 1)
                _pendingBytes -= _pending.Dequeue().Length;
        }
    }
}
