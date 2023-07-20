using OneWare.VcdViewer.ViewModels;
using OneWare.WaveFormViewer.Enums;
using OneWare.WaveFormViewer.Models;

namespace OneWare.VcdViewer.Models;

public class VcdSignal
{
    public SignalLineType Type { get; }
    public int BitWidth { get; }
    public char Id { get; }
    public string Name { get; }

    public List<VcdChange> Changes { get; } = new();

    public VcdSignal(SignalLineType type, int bitWidth, char id, string name)
    {
        Type = type;
        BitWidth = bitWidth;
        Id = id;
        Name = name;
    }
}