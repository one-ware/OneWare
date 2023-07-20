namespace OneWare.VcdViewer.Models;

public class VcdFile
{
    public long LastChangeTime { get; set; }
    public VcdDefinition Definition { get; }

    public VcdFile(VcdDefinition definition)
    {
        Definition = definition;
    }
}