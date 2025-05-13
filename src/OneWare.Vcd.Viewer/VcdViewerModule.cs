using Autofac;
using OneWare.Essentials.Services;
using OneWare.Vcd.Viewer.ViewModels;

namespace OneWare.Vcd.Viewer;

public static class VcdViewerModule
{
    public static void Register(ContainerBuilder builder)
    {
        builder.RegisterType<VcdViewModel>();
    }

    public static void Initialize(IContainer container)
    {
        var dockService = container.Resolve<IDockService>();
        var languageManager = container.Resolve<ILanguageManager>();
        var settingsService = container.Resolve<ISettingsService>();

        dockService.RegisterDocumentView<VcdViewModel>(".vcd");

        languageManager.RegisterLanguageExtensionLink(".vcdconf", ".json");

        settingsService.RegisterTitled("Simulator", "VCD Viewer",
            "VcdViewer_SaveView_Enable", "Enable Save File",
            "Enables storing view settings like open signals in a separate file", true);

        settingsService.RegisterSettingCategory("Simulator", 0, "Material.Pulse");

        settingsService.RegisterTitledCombo("Simulator", "VCD Viewer",
            "VcdViewer_LoadingThreads", "Loading Threads",
            "Sets amount of threads used to loading VCD Files", 1,
            Enumerable.Range(1, Environment.ProcessorCount).ToArray());
    }
}
