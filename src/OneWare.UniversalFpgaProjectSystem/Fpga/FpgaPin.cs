namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class FpgaPin
{
    public string Name { get; }
    
    public string? Description { get; }

    public FpgaPin(string name, string? description)
    {
        Name = name;
        Description = description;
    }
}