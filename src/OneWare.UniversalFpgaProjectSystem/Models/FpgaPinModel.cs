using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class FpgaPinModel : ObservableObject
{
    public FpgaPin Pin { get; }
    public FpgaModel Parent { get; }
    
    private string _toolTipText;
    public string ToolTipText
    {
        get => _toolTipText;
        set => SetProperty(ref _toolTipText, value);
    }
    
    private FpgaNodeModel? _connection;
    public FpgaNodeModel? Connection
    {
        get => _connection;
        set
        {
            this.SetProperty(ref _connection, value);
            if (_connection == null) ToolTipText = "Click to connect " + Pin.Name;
            else ToolTipText = Pin.Name + " is connected with " + _connection.FpgaNode.Name;
        }
    }
    
    public FpgaPinModel(FpgaPin pin, FpgaModel parent)
    {
        Pin = pin;
        _toolTipText = "Click to connect " + Pin.Name;
        Parent = parent;
    }
}