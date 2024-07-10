using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class FpgaNodeModel : ObservableObject
{
    public FpgaNode Node { get; }
    
    private FpgaPinModel? _connection;
    public FpgaPinModel? Connection
    {
        get => _connection;
        set => SetProperty(ref _connection, value);
    }

    public FpgaNodeModel(FpgaNode node)
    {
        Node = node;
    }

    public override string ToString()
    {
        return Connection is null ? Node.Name : $"{Node.Name} <-> {Connection.Pin.Name}";
    }
}