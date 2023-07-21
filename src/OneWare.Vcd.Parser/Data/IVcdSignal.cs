using System.Collections;

namespace OneWare.Vcd.Parser.Data;

public interface IVcdSignal
{
    public char Id { get; }

    public void AddChange(object change);
}