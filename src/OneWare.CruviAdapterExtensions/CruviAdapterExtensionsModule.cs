using Autofac;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.CruviAdapterExtensions;

public static class CruviAdapterExtensionsModule
{
    public static void Register(ContainerBuilder builder)
    {
        // No types to register in this module
    }

    public static void Initialize(IContainer container)
    {
        var fpgaService = container.Resolve<FpgaService>();

        fpgaService.RegisterFpgaExtensionPackage(new GenericFpgaExtensionPackage(
            "CRUVI_LS to PMOD Adapter", "CRUVI_LS",
            "avares://OneWare.CruviAdapterExtensions/Assets/CRUVI_LS/CRUVI_LS to PMOD Adapter"));

        fpgaService.RegisterFpgaExtensionPackage(new GenericFpgaExtensionPackage(
            "PMOD to CRUVI_LS Adapter", "PMOD",
            "avares://OneWare.CruviAdapterExtensions/Assets/PMOD/PMOD to CRUVI_LS Adapter"));
    }
}
