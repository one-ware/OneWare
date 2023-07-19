namespace OneWare.VcdViewer.Models;

public interface IScopeHolder
{
    public IScopeHolder? Parent { get; }
    public List<VcdScope> Scopes { get; }
    public List<VcdSignal> Signals { get; }
}