namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public interface IFpgaExtension
{
    public string Name { get; }

    public string Connector { get; }
    
    public IList<HardwarePin> Pins { get; }
    
    public IList<HardwareInterface> Interfaces { get; }
}