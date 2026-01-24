using Microsoft.Extensions.DependencyInjection;
using OneWare.Cyc5000.ViewModels;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.Cyc5000;

public class Cyc5000Module : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.Resolve<FpgaService>().RegisterFpgaPackage(new Cyc5000FpgaPackage());
    }
}

