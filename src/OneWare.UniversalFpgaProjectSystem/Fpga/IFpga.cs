namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public interface IFpga
{
    public string Name { get; }

    public IList<HardwarePin> Pins { get; }

    public IList<HardwareInterface> Interfaces { get; }

    public IReadOnlyDictionary<string, string> Properties { get; }

    /// <summary>
    /// Per-pin property definitions declared in the hardware JSON <c>allowedPinProperties</c> block.
    /// Returns an empty list for hardware that does not declare any properties.
    /// </summary>
    public IReadOnlyList<PinPropertyDefinition> AllowedPinProperties => [];
}