using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.VcdViewer.Parser;

namespace OneWare.VcdViewer.Models;

public class VcdScope : ObservableObject, IScopeHolder
{
    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
    
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