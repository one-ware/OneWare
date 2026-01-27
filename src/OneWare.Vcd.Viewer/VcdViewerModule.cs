using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Models;
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

        serviceProvider.Resolve<ISettingsService>().RegisterSetting("Simulator", "VCD Viewer",
            "VcdViewer_SaveView_Enable",
            new CheckBoxSetting("Enable Save File", true)
            {
                HoverDescription = "Enables storing view settings like open signals in a separate file"
            });

        serviceProvider.Resolve<ISettingsService>().RegisterSettingCategory("Simulator", 0, "Material.Pulse");
        serviceProvider.Resolve<ISettingsService>().RegisterSetting("Simulator", "VCD Viewer",
            "VcdViewer_LoadingThreads",
            new ComboBoxSetting("Loading Threads", 1,
                Enumerable.Range(1, Environment.ProcessorCount).Cast<object>().ToArray())
            {
                HoverDescription = "Sets amount of threads used to loading VCD Files"
            });
    }
}