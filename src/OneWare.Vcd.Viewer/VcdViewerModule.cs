using OneWare.SDK.Services;
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
        
        containerProvider.Resolve<ILanguageManager>().RegisterLanguageExtensionLink(".vcdconf", ".json");
        
        containerProvider.Resolve<ISettingsService>().RegisterTitled("Simulator", "VCD Viewer", "VcdViewer_SaveView_Enable", "Enable Save File", "Enables storing view settings like open signals in a separate file", true);
        
        containerProvider.Resolve<ISettingsService>().RegisterSettingCategory("Simulator", 0, "Material.Pulse");
        containerProvider.Resolve<ISettingsService>().RegisterTitledCombo("Simulator", "VCD Viewer", "VcdViewer_LoadingThreads", "Loading Threads", 
            "Sets amount of threads used to loading VCD Files", Math.Max(1,Environment.ProcessorCount/2), Enumerable.Range(1, Environment.ProcessorCount).ToArray());
    }
}