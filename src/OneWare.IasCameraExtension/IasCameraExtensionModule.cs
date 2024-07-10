using OneWare.IasCameraExtension.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.IasCameraExtension;

public class IasCameraExtensionModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var extension = new IasCameraExtension();
        
        containerProvider.Resolve<FpgaService>().RegisterFpgaExtension(extension);
        containerProvider.Resolve<FpgaService>().RegisterFpgaExtensionViewModel<IasCameraExtensionViewModel>(extension);
    }
}