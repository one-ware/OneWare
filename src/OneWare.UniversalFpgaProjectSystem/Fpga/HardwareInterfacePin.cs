namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class HardwareInterfacePin
{
    public HardwareInterfacePin(string name, HardwarePin pin)
    {
        Name = name;
        HardwarePin = pin;
    }

    public string Name { get; }

    public HardwarePin HardwarePin { get; }
}