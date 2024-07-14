namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class FpgaPin
{
    public FpgaPin(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    public string Name { get; }

    public string? Description { get; }
}