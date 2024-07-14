using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class FpgaNodeModel : ObservableObject
{
    private FpgaPinModel? _connection;

    public FpgaNodeModel(FpgaNode node)
    {
        Node = node;
    }

    public FpgaNode Node { get; }

    public FpgaPinModel? Connection
    {
        get => _connection;
        set => SetProperty(ref _connection, value);
    }

    public override string ToString()
    {
        return Connection is null ? Node.Name : $"{Node.Name} <-> {Connection.Pin.Name}";
    }
}