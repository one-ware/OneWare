namespace OneWare.Vcd.Parser.Data;

public class VcdSignal<T> : IVcdSignal
{
    public VcdLineType Type { get; }
    public int BitWidth { get; }
    public char Id { get; }
    public string Name { get; }

    public List<VcdChange<T>> Changes { get; } = new();

    public VcdSignal(VcdLineType type, int bitWidth, char id, string name)
    {
        Type = type;
        BitWidth = bitWidth;
        Id = id;
        Name = name;
    }
    
    public void AddChange(object change)
    {
        Changes.Add(change is VcdChange<T> vcdChange ? vcdChange : default);
    }
}