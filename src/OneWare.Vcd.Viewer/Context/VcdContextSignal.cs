using OneWare.WaveFormViewer.Enums;

namespace OneWare.Vcd.Viewer.Context;

public class VcdContextSignal(string id, WaveDataType dataType, bool automaticFixedPointShift, int fixedPointShift)
{
    public string Id { get; } = id;

    /// <summary>
    ///     Signal name, used to find the signal when a profile is loaded for a VCD file with different ids.
    /// </summary>
    public string? Name { get; init; }

    public WaveDataType DataType { get; } = dataType;

    public bool AutomaticFixedPointShift { get; } = automaticFixedPointShift;

    public int FixedPointShift { get; } = fixedPointShift;

    public string? SeparatorLabel { get; init; }
}
