using OneWare.Cyc5000.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Modularity;

namespace OneWare.Cyc5000;

public class Cyc5000Module 
{
    FpgaService _fpgaService

    public Cyc5000Module(FpgaService fpgaService)
    {
        _fpgaService = fpgaService;
    }
    public void OnInitialized()
    {
        _fpgaService.RegisterFpgaPackage(new Cyc5000FpgaPackage());
    }
}