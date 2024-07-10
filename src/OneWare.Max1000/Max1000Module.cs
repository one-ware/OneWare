using OneWare.Max1000.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Max1000;

public class Max1000Module : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var fpga = new Max1000Fpga();
        var fpga2 = new Max100016KFpga();
        containerProvider.Resolve<FpgaService>().RegisterFpga(fpga);
        containerProvider.Resolve<FpgaService>().RegisterFpga(fpga2);
        containerProvider.Resolve<FpgaService>().RegisterCustomFpgaViewModel<Max1000ViewModel>(fpga);
        containerProvider.Resolve<FpgaService>().RegisterCustomFpgaViewModel<Max1000ViewModel>(fpga2);
    }
}