using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Services;
using OneWare.Vcd.Viewer.ViewModels;

namespace OneWare.Vcd.Viewer;

public class VcdViewerModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<VcdViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.Resolve<IMainDockService>().RegisterDocumentView<VcdViewModel>(".vcd");

        serviceProvider.Resolve<ILanguageManager>().RegisterLanguageExtensionLink(".vcdconf", ".json");

        serviceProvider.Resolve<ISettingsService>().RegisterTitled("Simulator", "VCD Viewer",
            "VcdViewer_SaveView_Enable", "Enable Save File",
            "Enables storing view settings like open signals in a separate file", true);

        serviceProvider.Resolve<ISettingsService>().RegisterSettingCategory("Simulator", 0, "Material.Pulse");
        serviceProvider.Resolve<ISettingsService>().RegisterTitledCombo("Simulator", "VCD Viewer",
            "VcdViewer_LoadingThreads", "Loading Threads",
            "Sets amount of threads used to loading VCD Files", 1,
            Enumerable.Range(1, Environment.ProcessorCount).ToArray());
    }
}

