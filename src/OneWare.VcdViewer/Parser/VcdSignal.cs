using ImTools;

namespace OneWare.VcdViewer.Parser;

public class VcdSignal
{
    public string Type { get; }
    
    public int Bitwidth { get; }
    
    public string Id { get; }
    public string Name { get; }

    public VcdSignal(string type, int bitwidth, string id, string name)
    {
        Type = type;
        Bitwidth = bitwidth;
        Id = id;
        Name = name;
    }
}