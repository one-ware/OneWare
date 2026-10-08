using Xunit;

namespace OneWare.WaveFormViewer.Tests;

public class SignalEdgeHelperTests
{
    private readonly List<long> _changeTimes = [0, 10, 20, 30];

    [Theory]
    [InlineData(0, 10L)]
    [InlineData(5, 10L)]
    [InlineData(10, 30L)]
    [InlineData(29, 30L)]
    [InlineData(30, null)]
    [InlineData(100, null)]
    public void FindNextEdge(long offset, long? expected)
    {
        var signal = TestSignals.CreateBit("a", _changeTimes, 0, 10, 30);

        Assert.Equal(expected, SignalEdgeHelper.FindNextEdge(signal, offset));
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(5, 0L)]
    [InlineData(10, 0L)]
    [InlineData(25, 10L)]
    [InlineData(30, 10L)]
    [InlineData(100, 30L)]
    public void FindPreviousEdge(long offset, long? expected)
    {
        var signal = TestSignals.CreateBit("a", _changeTimes, 0, 10, 30);

        Assert.Equal(expected, SignalEdgeHelper.FindPreviousEdge(signal, offset));
    }

    [Fact]
    public void FindEdge_BeforeFirstChange()
    {
        var signal = TestSignals.CreateBit("a", _changeTimes, 10, 20);

        Assert.Equal(10, SignalEdgeHelper.FindNextEdge(signal, 0));
        Assert.Null(SignalEdgeHelper.FindPreviousEdge(signal, 5));
    }

    [Fact]
    public void FindEdge_WithoutChanges_ReturnsNull()
    {
        var signal = TestSignals.CreateBit("a", _changeTimes);

        Assert.Null(SignalEdgeHelper.FindNextEdge(signal, 0));
        Assert.Null(SignalEdgeHelper.FindPreviousEdge(signal, 100));
    }

    [Fact]
    public void FindEdge_UsesClosestEdgeOfAllSignals()
    {
        var a = TestSignals.CreateBit("a", _changeTimes, 0, 30);
        var b = TestSignals.CreateBit("b", _changeTimes, 0, 20);

        Assert.Equal(20, SignalEdgeHelper.FindEdge([a, b], 5, true));
        Assert.Equal(20, SignalEdgeHelper.FindEdge([a, b], 25, false));
        Assert.Null(SignalEdgeHelper.FindEdge([a, b], 30, true));
    }
}
