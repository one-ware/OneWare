namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public interface IFpga
{
    public string Name { get; }
    
    public IList<FpgaPin> Pins { get; }
}