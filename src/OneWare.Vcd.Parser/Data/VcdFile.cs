namespace OneWare.Vcd.Parser.Data;

public class VcdFile
{
    public long LastChangeTime { get; set; }
    public VcdDefinition Definition { get; }

    public VcdFile(VcdDefinition definition)
    {
        Definition = definition;
    }
}