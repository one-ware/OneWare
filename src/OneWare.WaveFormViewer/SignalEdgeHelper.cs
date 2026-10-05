using OneWare.Vcd.Parser.Data;

namespace OneWare.WaveFormViewer;

public static class SignalEdgeHelper
{
    /// <summary>
    ///     Returns the time of the first value change after <paramref name="offset" />, or <see langword="null" />.
    /// </summary>
    public static long? FindNextEdge(IVcdSignal signal, long offset)
    {
        for (var i = Math.Max(signal.FindIndex(offset), 0);; i++)
        {
            var time = signal.GetChangeTimeFromIndex(i);
            if (time == long.MaxValue) return null;
            if (time > offset) return time;
        }
    }

    /// <summary>
    ///     Returns the time of the last value change before <paramref name="offset" />, or <see langword="null" />.
    /// </summary>
    public static long? FindPreviousEdge(IVcdSignal signal, long offset)
    {
        for (var i = signal.FindIndex(offset); i >= 0; i--)
        {
            var time = signal.GetChangeTimeFromIndex(i);
            if (time < offset) return time;
        }

        return null;
    }

    /// <summary>
    ///     Returns the closest edge after (<paramref name="forward" />) or before <paramref name="offset" /> across all
    ///     <paramref name="signals" />, or <see langword="null" /> if there is none.
    /// </summary>
    public static long? FindEdge(IEnumerable<IVcdSignal> signals, long offset, bool forward)
    {
        long? result = null;
        foreach (var signal in signals)
        {
            var edge = forward ? FindNextEdge(signal, offset) : FindPreviousEdge(signal, offset);
            if (edge is not { } time) continue;
            if (result is not { } best || (forward ? time < best : time > best)) result = time;
        }

        return result;
    }
}
