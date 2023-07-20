using OneWare.VcdViewer.ViewModels;
using OneWare.WaveFormViewer.Models;

namespace OneWare.VcdViewer.Models;

public class VcdSignal
{
    public string Type { get; }
    public int BitWidth { get; }
    public char Id { get; }
    public string Name { get; }

    public List<VcdChange> Changes { get; } = new();

    public VcdSignal(string type, int bitWidth, char id, string name)
    {
        Type = type;
        BitWidth = bitWidth;
        Id = id;
        Name = name;
    }
}