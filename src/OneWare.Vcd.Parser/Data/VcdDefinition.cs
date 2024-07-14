namespace OneWare.Vcd.Parser.Data;

public class VcdDefinition : IScopeHolder
{
    public List<long> ChangeTimes { get; } = new();
    public long TimeScale { get; set; } = 1; // 1 = 1 FS
    public Dictionary<string, IVcdSignal> SignalRegister { get; } = new();
    public IScopeHolder? Parent => null;
    public List<VcdScope> Scopes { get; } = new();
    public List<IVcdSignal> Signals { get; } = new();
}