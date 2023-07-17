namespace OneWare.VcdViewer.Parser;

public class VcdScope
{
    public string Name { get; }
    public List<VcdSignal> Signals = new();

    public VcdScope(string name)
    {
        Name = name;
    }
}