namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class HardwarePin
{
    public HardwarePin(string name, string? description, string? interfacePin = null)
    {
        Name = name;
        Description = description;
        InterfacePin = interfacePin;
    }

    public string Name { get; }
    
    public string? InterfacePin { get; }

    public string? Description { get; }
}