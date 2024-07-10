using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.TangNano9K;

public class TangNano9KModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var fpga = new TangNano9KFpga();
        containerProvider.Resolve<FpgaService>().RegisterFpga(fpga);
    }
}