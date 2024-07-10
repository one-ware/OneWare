namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class FpgaInterfacePin
{
    public string Name { get; }
    
    public FpgaPin FpgaPin { get; }

    public FpgaInterfacePin(string name, FpgaPin pin)
    {
        this.Name = name;
        this.FpgaPin = pin;
    }
}