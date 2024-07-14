namespace OneWare.Vcd.Parser.Data;

public class VcdScope : IScopeHolder
{
    public VcdScope(IScopeHolder parent, string name)
    {
        Parent = parent;
        Name = name;
    }

    public string Name { get; }
    public IScopeHolder? Parent { get; }
    public List<VcdScope> Scopes { get; } = new();
    public List<IVcdSignal> Signals { get; } = new();
}