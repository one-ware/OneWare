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
        containerProvider.Resolve<FpgaService>().RegisterFpgaPackage(new Max1000FpgaPackage());
        containerProvider.Resolve<FpgaService>().RegisterFpgaPackage(new Max100016KFpgaPackage());
    }
}