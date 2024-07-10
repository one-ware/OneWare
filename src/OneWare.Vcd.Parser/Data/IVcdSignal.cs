namespace OneWare.Vcd.Parser.Data;

public interface IVcdSignal
{
    public event EventHandler RequestRedraw;
    public Type ValueType { get; }
    public VcdLineType Type { get; }
    public string Name { get; }
    public string Id { get; }
    public int BitWidth { get; }
    public void AddChange(int timeIndex, dynamic change);
    public void AddChanges(IVcdSignal signal);
    public void RemoveChangeAtIndex(int changeTimeIndex);
    public void Clear();
    public int FindIndex(long offset);
    public object? GetValueFromOffset(long offset);
    public long GetChangeTimeFromIndex(int index);
    public object? GetValueFromIndex(int index);
    public void Invalidate();
    public IVcdSignal CloneEmpty();
}