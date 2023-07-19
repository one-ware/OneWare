using OneWare.VcdViewer.Parser;

namespace OneWare.VcdViewer.Models;

public class VcdScope : IScopeHolder
{
    public IScopeHolder? Parent { get; }
    public List<VcdScope> Scopes { get; } = new();
    public List<VcdSignal> Signals { get; } = new();
    public string Name { get; }

    public VcdScope(IScopeHolder parent, string name)
    {
        Parent = parent;
        Name = name;
    }
}