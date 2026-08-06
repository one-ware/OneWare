using System.Text;
using Avalonia.Threading;
using OneWare.Terminal.Provider;
using VtNetCore.Avalonia;
using Xunit;

namespace OneWare.Terminal.UnitTests;

public class ReplayBufferedConnectionTests
{
    [Fact]
    public void ReplaysOutputProducedBeforeAControlSubscribed()
    {
        var inner = new FakeConnection();
        using var connection = new ReplayBufferedConnection(inner);

        inner.Emit("while-hidden");

        var received = Subscribe(connection, out _);
        Assert.Empty(received);

        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["while-hidden"], received);
    }

    [Fact]
    public void ForwardsOutputWhileAControlIsSubscribed()
    {
        var inner = new FakeConnection();
        using var connection = new ReplayBufferedConnection(inner);
        var received = Subscribe(connection, out _);

        inner.Emit("live");

        Assert.Equal(["live"], received);
    }

    [Fact]
    public void BuffersWhileDetachedAndReplaysInOrderOnReattach()
    {
        var inner = new FakeConnection();
        using var connection = new ReplayBufferedConnection(inner);

        var firstControl = Subscribe(connection, out var firstHandler);
        inner.Emit("visible");

        connection.DataReceived -= firstHandler;
        inner.Emit("hidden-1");
        inner.Emit("hidden-2");
        Assert.Equal(["visible"], firstControl);

        var secondControl = Subscribe(connection, out _);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["hidden-1", "hidden-2"], secondControl);

        // The detached control must not receive anything it missed.
        Assert.Equal(["visible"], firstControl);
    }

    [Fact]
    public void KeepsLiveOutputBehindAPendingReplay()
    {
        var inner = new FakeConnection();
        using var connection = new ReplayBufferedConnection(inner);

        inner.Emit("buffered");
        var received = Subscribe(connection, out _);
        inner.Emit("live");

        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["buffered", "live"], received);
    }

    [Fact]
    public void DoesNotReplayToASecondSubscriberOfAlreadyDeliveredOutput()
    {
        var inner = new FakeConnection();
        using var connection = new ReplayBufferedConnection(inner);
        Subscribe(connection, out _);
        inner.Emit("delivered");

        var second = Subscribe(connection, out _);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(second);
    }

    [Fact]
    public void DropsTheOldestOutputWhenTheBufferOverflows()
    {
        var inner = new FakeConnection();
        using var connection = new ReplayBufferedConnection(inner);

        inner.Emit("oldest");
        for (var i = 0; i < 3; i++) inner.Emit(new string('x', 512 * 1024));

        var received = Subscribe(connection, out _);
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("oldest", received);
        Assert.NotEmpty(received);
    }

    [Fact]
    public void StopsDeliveringAfterDispose()
    {
        var inner = new FakeConnection();
        var connection = new ReplayBufferedConnection(inner);
        var received = Subscribe(connection, out _);

        connection.Dispose();
        inner.Emit("after-dispose");
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(received);
        Assert.Empty(inner.DataReceivedSubscribers);
    }

    [Fact]
    public void ForwardsTheConnectionApiToTheInnerConnection()
    {
        var inner = new FakeConnection();
        using var connection = new ReplayBufferedConnection(inner);

        connection.SendData([1, 2, 3]);
        connection.SetTerminalWindowSize(120, 40);
        var closed = false;
        connection.Closed += (_, _) => closed = true;
        connection.Disconnect();

        Assert.True(connection.IsConnected);
        Assert.Equal([1, 2, 3], Assert.Single(inner.Sent));
        Assert.Equal((120, 40), inner.WindowSize);
        Assert.True(closed);
    }

    private static List<string> Subscribe(ReplayBufferedConnection connection,
        out EventHandler<DataReceivedEventArgs> handler)
    {
        var received = new List<string>();
        handler = (_, e) => received.Add(Encoding.UTF8.GetString(e.Data));
        connection.DataReceived += handler;
        return received;
    }

    private sealed class FakeConnection : IConnection
    {
        public List<byte[]> Sent { get; } = [];
        public (int Columns, int Rows) WindowSize { get; private set; }
        public bool IsConnected => true;

        public List<Delegate> DataReceivedSubscribers =>
            DataReceived?.GetInvocationList().ToList() ?? [];

        public event EventHandler<DataReceivedEventArgs>? DataReceived;
        public event EventHandler<EventArgs>? Closed;

        public bool Connect()
        {
            return true;
        }

        public void Disconnect()
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }

        public void SendData(byte[] data)
        {
            Sent.Add(data);
        }

        public void SetTerminalWindowSize(int columns, int rows)
        {
            WindowSize = (columns, rows);
        }

        public void Emit(string text)
        {
            DataReceived?.Invoke(this, new DataReceivedEventArgs { Data = Encoding.UTF8.GetBytes(text) });
        }
    }
}
