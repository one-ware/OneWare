using System.Collections;

namespace OneWare.Vcd.Parser.Data;

public interface IVcdSignal
{
    public VcdLineType Type { get; }
    public string Name { get; }
    public char Id { get; }
    public void AddChange(int time, object change);
    public void Clear();
}