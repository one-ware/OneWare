using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class NodeModel : ObservableObject
{
    public string Name { get; }
    
    public string Direction { get; }
    
    
    private FpgaPinModel? _connection;
    public FpgaPinModel? Connection
    {
        get => _connection;
        set
        {
            this.SetProperty(ref _connection, value);
        }
    }

    public NodeModel(string name,  string direction)
    {
        Name = name;
        Direction = direction;
    }
    
    public override string ToString()
    {
        return Name;
    }
}