namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class HardwareInterfacePin
{
    public HardwareInterfacePin(string name, string bindPin)
    {
        Name = name;
        BindPin = bindPin;
    }

    public string Name { get; }

    public string BindPin { get; }
}