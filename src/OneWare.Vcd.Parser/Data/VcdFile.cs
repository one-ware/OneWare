namespace OneWare.Vcd.Parser.Data;

public class VcdFile
{
    public List<long> ChangeTimes { get; } = new();
    public VcdDefinition Definition { get; }

    public VcdFile(VcdDefinition definition)
    {
        Definition = definition;
    }

    public IVcdSignal? GetSignal(char id)
    {
        return GetSignal(Definition, id);
    }
    
    private IVcdSignal? GetSignal(IScopeHolder holder, char id)
    {
        if (holder.Signals.FirstOrDefault(x => x.Id == id) is { } s) return s;
        foreach (var sc in holder.Scopes)
        {
            if (GetSignal(sc, id) is {} vcd) return vcd;
        }
        return null;
    }
}