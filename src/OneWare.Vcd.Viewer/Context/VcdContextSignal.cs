using OneWare.WaveFormViewer.Enums;

namespace OneWare.Vcd.Viewer.Context;

public class VcdContextSignal(string id, WaveDataType dataType, bool automaticFixedPointShift, int fixedPointShift)
{
    public string Id { get; } = id;
    
    public WaveDataType DataType { get; } = dataType;
    
    public bool AutomaticFixedPointShift { get; } = automaticFixedPointShift;

    public int FixedPointShift { get; } = fixedPointShift;
}