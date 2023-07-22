namespace OneWare.Vcd.Parser.Data;

public interface IVcdSignal
{
    public VcdLineType Type { get; }
    public string Name { get; }
    public char Id { get; }
    public void AddChange(int time, object change);
    public void Clear();
    public int FindIndex(long offset);
    public object? GetValueFromOffset(long offset);
    public long GetChangeTimeFromIndex(int index);
    public object? GetValueFromIndex(int index);
}