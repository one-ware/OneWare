namespace OneWare.VcdViewer.Models;

public struct VcdChange
{
    public long Time { get; init; }
    
    public object Value { get; init; }
}