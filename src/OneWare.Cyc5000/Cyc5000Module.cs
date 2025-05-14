using OneWare.Cyc5000.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Services;
using Autofac;

namespace OneWare.Cyc5000
{
    public class Cyc5000Module
    {
        public void RegisterTypes(ContainerBuilder builder)
        {
            // Register types with Autofac container
            // For example:
            // builder.RegisterType<FpgaService>().AsSelf();
        }

        public void OnInitialized(IContainer container)
        {
            // Resolve dependencies using Autofac
            var fpgaService = container.Resolve<FpgaService>();
            fpgaService.RegisterFpgaPackage(new Cyc5000FpgaPackage());
        }
    }
}
