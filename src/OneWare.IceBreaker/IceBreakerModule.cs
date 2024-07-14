using OneWare.IceBreaker.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.IceBreaker;

public class IceBreakerModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var fpga = new IceBreakerFpga();
        containerProvider.Resolve<FpgaService>().RegisterFpga(fpga);
        containerProvider.Resolve<FpgaService>().RegisterCustomFpgaViewModel<IceBreakerFpgaViewModel>(fpga);
    }
}