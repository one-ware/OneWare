namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class HardwareInterface(string name, string? connector)
{
    public string Name { get; } = name;

    public string? Connector { get; } = connector;

    public IList<HardwareInterfacePin> Pins { get; } = new List<HardwareInterfacePin>();
}