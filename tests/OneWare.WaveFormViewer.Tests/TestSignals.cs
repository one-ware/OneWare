using OneWare.Vcd.Parser.Data;

namespace OneWare.WaveFormViewer.Tests;

internal static class TestSignals
{
    /// <summary>
    ///     Creates a 1 bit signal that changes its value at the given times. All times must exist in
    ///     <paramref name="changeTimes" />.
    /// </summary>
    public static VcdSignal<StdLogic> CreateBit(string id, List<long> changeTimes, params long[] edges)
    {
        var signal = new VcdSignal<StdLogic>(changeTimes, VcdLineType.Wire, 1, id, id);
        var value = StdLogic.Zero;
        foreach (var edge in edges)
        {
            signal.AddChange(changeTimes.IndexOf(edge), value);
            value = value == StdLogic.Zero ? StdLogic.Full : StdLogic.Zero;
        }

        return signal;
    }

    /// <summary>
    ///     Creates an 8 bit signal without changes.
    /// </summary>
    public static VcdSignal<StdLogic[]> CreateBus(string id, List<long> changeTimes)
    {
        return new VcdSignal<StdLogic[]>(changeTimes, VcdLineType.Wire, 8, id, id);
    }
}
