namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class HardwarePin
{
    private static readonly IReadOnlyDictionary<string, string> EmptyProperties =
        new Dictionary<string, string>();

    public HardwarePin(string name, string? description, string? interfacePin = null,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        Name = name;
        Description = description;
        InterfacePin = interfacePin;
        Properties = properties ?? EmptyProperties;
    }

    public string Name { get; }

    public string? InterfacePin { get; }

    public string? Description { get; }

    /// <summary>
    /// Hardware-defined static per-pin metadata parsed from <c>fpga.json</c>.
    /// These serve as default values for <c>HardwarePinModel.PinPropertyValues</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; }
}