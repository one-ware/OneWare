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
        containerProvider.Resolve<FpgaService>().RegisterFpgaPackage(new Max10FpgaPackage());
        containerProvider.Resolve<FpgaService>().RegisterFpgaPackage(new Max10UltraFpgaPackage());
    }
}