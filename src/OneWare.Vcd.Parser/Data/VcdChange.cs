using System.Drawing;

namespace OneWare.Vcd.Parser.Data;

public struct VcdChange<T>
{
    public long Time { get; }
    public T Data { get; }
    
    public VcdChange(long time, T data)
    {
        Time = time;
        Data = data;
    }
}