namespace OneWare.Vcd.Viewer.Context;

public class VcdContext(IEnumerable<VcdContextSignal> openSignals)
{
    public IEnumerable<VcdContextSignal>? OpenSignals { get; } = openSignals;
}