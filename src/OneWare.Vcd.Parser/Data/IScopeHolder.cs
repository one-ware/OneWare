namespace OneWare.Vcd.Parser.Data;

public interface IScopeHolder
{
    public IScopeHolder? Parent { get; }
    public List<VcdScope> Scopes { get; }
    public List<IVcdSignal> Signals { get; }
}