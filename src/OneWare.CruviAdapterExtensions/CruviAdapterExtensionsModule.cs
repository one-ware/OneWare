using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.CruviAdapterExtensions;

public class CruviAdapterExtensionsModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<FpgaService>().RegisterFpgaExtensionPackage(new GenericFpgaExtensionPackage("CRUVI_LS to PMOD Adapter", "CRUVI_LS", "avares://OneWare.CruviAdapterExtensions/Assets/CRUVI_LS/CRUVI_LS to PMOD Adapter"));
        containerProvider.Resolve<FpgaService>().RegisterFpgaExtensionPackage(new GenericFpgaExtensionPackage("PMOD to CRUVI_LS Adapter", "PMOD", "avares://OneWare.CruviAdapterExtensions/Assets/PMOD/PMOD to CRUVI_LS Adapter"));
    }
}