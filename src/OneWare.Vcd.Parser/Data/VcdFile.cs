namespace OneWare.Vcd.Parser.Data;

public class VcdFile
{
    public VcdFile(VcdDefinition definition)
    {
        Definition = definition;
    }

    public long DefinitionParseEndPosition { get; set; }
    public VcdDefinition Definition { get; }

    public IVcdSignal? GetSignal(string id)
    {
        return GetSignal(Definition, id);
    }

    private IVcdSignal? GetSignal(IScopeHolder holder, string id)
    {
        if (holder.Signals.FirstOrDefault(x => x.Id == id) is { } s) return s;
        foreach (var sc in holder.Scopes)
            if (GetSignal(sc, id) is { } vcd)
                return vcd;
        return null;
    }
}