using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class FpgaPinModel : ObservableObject
{
    public FpgaModel Parent { get; }
    public string Name { get; }

    private string _description;
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private string _toolTipText;
    public string ToolTipText
    {
        get => _toolTipText;
        set => SetProperty(ref _toolTipText, value);
    }
    
    private NodeModel? _connection;
    public NodeModel? Connection
    {
        get => _connection;
        set
        {
            this.SetProperty(ref _connection, value);
            if (_connection == null) ToolTipText = "Click to connect " + Name;
            else ToolTipText = Name + " is connected with " + _connection.Name;
        }
    }
    
    public FpgaPinModel(string name, string description, FpgaModel parent)
    {
        Name = name;
        Description = description;
        Parent = parent;
    }
}