using OneWare.Shared.Services;
using OneWare.VcdViewer.ViewModels;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.VcdViewer;

public class VcdViewerModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.Register<VcdViewModel>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<IDockService>().RegisterDocumentView<VcdViewModel>(".vcd");
    }
}