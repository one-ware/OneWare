using OneWare.VcdViewer.ViewModels;

namespace OneWare.VcdViewer.Models;

public class VcdSignal
{
    public string Type { get; }
    public int BitWidth { get; }
    public string Id { get; }
    public string Name { get; }

    public VcdSignal(string type, int bitWidth, string id, string name)
    {
        Type = type;
        BitWidth = bitWidth;
        Id = id;
        Name = name;
    }
}