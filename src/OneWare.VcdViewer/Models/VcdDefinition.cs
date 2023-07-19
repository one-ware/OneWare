using OneWare.VcdViewer.Parser;

namespace OneWare.VcdViewer.Models;

public class VcdDefinition : IScopeHolder
{
    public IScopeHolder? Parent => null;
    public string? TimeScale { get; set; }
    public List<VcdScope> Scopes { get; } = new();
    public List<VcdSignal> Signals { get; } = new();
}