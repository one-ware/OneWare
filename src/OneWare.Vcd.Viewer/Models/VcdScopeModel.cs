using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Shared.Models;
using OneWare.Vcd.Parser.Data;

namespace OneWare.Vcd.Viewer.Models;

public class VcdScopeModel : ObservableObject
{
    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
    public string Name { get; }
    public IEnumerable<VcdScopeModel> Scopes { get; init; }
    public List<IVcdSignal> Signals { get; }

    public VcdScopeModel(VcdScope scope)
    {
        Name = scope.Name.Split(' ').Last();
        Scopes = scope.Scopes.Where(x => x.Signals.Any() || x.Scopes.Any()).Select(x => new VcdScopeModel(x));
        Signals = scope.Signals;
    }
}