using Avalonia.Threading;
using VtNetCore.Avalonia;

namespace OneWare.Terminal.Provider;

/// <summary>
/// Wraps a pty connection for the terminal control. The control subscribes to
/// <see cref="DataReceived"/> only while it is attached to the visual tree, and the terminal
/// pane keeps just the selected tab attached. Without this wrapper every byte a shell writes
/// while its tab is not selected is dropped, so the terminal shows a hole in its output (or
/// nothing at all for commands the AI ran in a background tab).
/// This wrapper keeps that output and replays it once a control subscribes again.
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
    private bool _flushScheduled;
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
                _dataReceived += value;
                ScheduleFlush();
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

            // While a replay is still pending, new data has to queue up behind it.
            if (_dataReceived != null && _pending.Count == 0)
            {
                _dataReceived(this, e);
                return;
            }

            _pending.Enqueue(e.Data);
            _pendingBytes += e.Data.Length;

            // Drop the oldest output when a hidden terminal produces more than the buffer
            // holds; that part would have scrolled out of the terminal's scrollback anyway.
            while (_pendingBytes > MaxBufferedBytes && _pending.Count > 1)
                _pendingBytes -= _pending.Dequeue().Length;

            ScheduleFlush();
        }
    }

    /// <summary>
    /// Replays buffered output on the UI thread instead of inline. A subscribing terminal
    /// control is not ready to consume data yet: it binds its connection before its terminal
    /// controller, and only the latter creates the parser the data is pushed into.
    /// </summary>
    private void ScheduleFlush()
    {
        if (_flushScheduled || _disposed || _dataReceived == null || _pending.Count == 0) return;

        _flushScheduled = true;
        Dispatcher.UIThread.Post(Flush);
    }

    private void Flush()
    {
        lock (_lock)
        {
            _flushScheduled = false;
            if (_disposed) return;

            var handler = _dataReceived;
            if (handler == null) return;

            while (_pending.Count > 0)
                handler(this, new DataReceivedEventArgs { Data = _pending.Dequeue() });

            _pendingBytes = 0;
        }
    }
}
