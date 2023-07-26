using OneWare.Shared.Services;
using OneWare.Vcd.Viewer.ViewModels;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Vcd.Viewer;

public class VcdViewerModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.Register<VcdViewModel>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<IDockService>().RegisterDocumentView<VcdViewModel>(".vcd");
        
        containerProvider.Resolve<ISettingsService>().RegisterTitledCombo("Simulator", "Vcd Viewer", "VcdViewer_LoadingThreads", "Loading Threads", 
            "Sets amount of threads used to loading VCD Files", 4, Enumerable.Range(1, Environment.ProcessorCount).ToArray());
}
}