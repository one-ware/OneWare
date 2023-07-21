namespace OneWare.Vcd.Parser.Data;

public class VcdSignal<T> : IVcdSignal
{
    public VcdLineType Type { get; }
    public int BitWidth { get; }
    public char Id { get; }
    public string Name { get; }
    private List<int> ChangeTimes { get; } = new();
    private List<T?> Changes { get; } = new();

    public VcdSignal(VcdLineType type, int bitWidth, char id, string name)
    {
        Type = type;
        BitWidth = bitWidth;
        Id = id;
        Name = name;
    }
    
    public void AddChange(int timeIndex, object change)
    {
        ChangeTimes.Add(timeIndex);
        Changes.Add(change is T val ? val : default);
    }

    public void Clear()
    {
        Changes.Clear();
        Changes.TrimExcess();
    }
}