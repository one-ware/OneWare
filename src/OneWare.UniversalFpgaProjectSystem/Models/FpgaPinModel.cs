using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class FpgaPinModel : ObservableObject
{
    private FpgaNodeModel? _connection;

    private string _toolTipText;

    private bool _isSelected;

    public FpgaPinModel(FpgaPin pin, FpgaModel parent)
    {
        Pin = pin;
        _toolTipText = "Click to connect " + Pin.Name;
        Parent = parent;
    }

    public FpgaPin Pin { get; }
    public FpgaModel Parent { get; }

    public string ToolTipText
    {
        get => _toolTipText;
        set => SetProperty(ref _toolTipText, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
    
    public FpgaNodeModel? Connection
    {
        get => _connection;
        set
        {
            SetProperty(ref _connection, value);
            if (_connection == null) ToolTipText = "Click to connect " + Pin.Name;
            else ToolTipText = Pin.Name + " is connected with " + _connection.Node.Name;
        }
    }

    public override string ToString()
    {
        return Connection is null ? Pin.Name : $"{Pin.Name} <-> {Connection.Node.Name}";
    }
}