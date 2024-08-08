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
        containerProvider.Resolve<FpgaService>().RegisterFpgaPackage(new Cyc5000FpgaPackage());
    }
}