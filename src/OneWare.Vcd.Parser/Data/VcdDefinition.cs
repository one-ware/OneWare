namespace OneWare.Vcd.Parser.Data;

public class VcdDefinition : IScopeHolder
{
    public List<long> ChangeTimes { get; } = new();
    public IScopeHolder? Parent => null;
    public string? TimeScale { get; set; }
    public List<VcdScope> Scopes { get; } = new();
    public List<IVcdSignal> Signals { get; } = new();
    public Dictionary<char, IVcdSignal> SignalRegister { get; } = new();
}