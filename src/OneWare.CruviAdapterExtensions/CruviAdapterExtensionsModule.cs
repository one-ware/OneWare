using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.CruviAdapterExtensions;

public class CruviAdapterExtensionsModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.Resolve<FpgaService>().RegisterFpgaExtensionPackage(
            new GenericFpgaExtensionPackage("CRUVI_LS to PMOD Adapter", "CRUVI_LS",
                "avares://OneWare.CruviAdapterExtensions/Assets/CRUVI_LS/CRUVI_LS to PMOD Adapter"));
        serviceProvider.Resolve<FpgaService>().RegisterFpgaExtensionPackage(
            new GenericFpgaExtensionPackage("PMOD to CRUVI_LS Adapter", "PMOD",
                "avares://OneWare.CruviAdapterExtensions/Assets/PMOD/PMOD to CRUVI_LS Adapter"));
    }
}

