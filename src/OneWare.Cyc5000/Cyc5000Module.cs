using OneWare.Cyc5000.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Cyc5000;

public class Cyc5000Module : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var fpga = new Cyc5000Fpga();
        containerProvider.Resolve<FpgaService>().RegisterFpga(fpga);
        containerProvider.Resolve<FpgaService>().RegisterCustomFpgaViewModel<Cyc5000ViewModel>(fpga);
    }
}