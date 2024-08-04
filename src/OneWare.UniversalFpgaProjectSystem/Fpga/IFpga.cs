namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public interface IFpga
{
    public string Name { get; }

    public IList<HardwarePin> Pins { get; }

    public IList<HardwareInterface> Interfaces { get; }

    public IReadOnlyDictionary<string, string> Properties { get; }
}