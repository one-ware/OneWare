using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public interface IFpgaPackage
{
    public string Name { get; }
    
    public IFpga LoadFpga();
    
    public FpgaViewModelBase? LoadFpgaViewModel(FpgaModel fpgaModel);
}