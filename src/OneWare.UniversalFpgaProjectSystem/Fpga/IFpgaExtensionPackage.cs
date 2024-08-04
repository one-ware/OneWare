using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public interface IFpgaExtensionPackage
{
    public string Name { get; }
    
    public string Connector { get; }
    
    public IFpgaExtension LoadExtension();
    
    public ExtensionViewModelBase? LoadExtensionViewModel(FpgaExtensionModel fpgaExtensionModel);
}