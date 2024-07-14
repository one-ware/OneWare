namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class FpgaInterfacePin
{
    public FpgaInterfacePin(string name, FpgaPin pin)
    {
        Name = name;
        FpgaPin = pin;
    }

    public string Name { get; }

    public FpgaPin FpgaPin { get; }
}