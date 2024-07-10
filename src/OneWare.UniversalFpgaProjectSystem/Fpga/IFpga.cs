using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public interface IFpga
{
    public string Name { get; }
    
    public IList<FpgaPin> Pins { get; }
    
    public IList<FpgaInterface> Interfaces { get; }
    
    public IReadOnlyDictionary<string, string> Properties { get; }
}