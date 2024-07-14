using OneWare.Max10.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Max10;

public class Max10Module : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var fpga = new Max10Fpga();
        var fpgaUltra = new Max10UltraFpga();
        containerProvider.Resolve<FpgaService>().RegisterFpga(fpga);
        containerProvider.Resolve<FpgaService>().RegisterFpga(fpgaUltra);
        containerProvider.Resolve<FpgaService>().RegisterCustomFpgaViewModel<Max10FpgaViewModel>(fpga);
        containerProvider.Resolve<FpgaService>().RegisterCustomFpgaViewModel<Max10FpgaViewModel>(fpgaUltra);
    }
}