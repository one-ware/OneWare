namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class FpgaInterface(string name, string? connector)
{
    public string Name { get; } = name;

    public string? Connector { get; } = connector;

    public IList<FpgaInterfacePin> Pins { get; } = new List<FpgaInterfacePin>();
}