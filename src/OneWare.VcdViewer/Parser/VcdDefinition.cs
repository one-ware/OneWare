namespace OneWare.VcdViewer.Parser;

public class VcdDefinition
{
    public string? TimeScale { get; set; }
    public List<VcdScope> Scopes { get; } = new();
}