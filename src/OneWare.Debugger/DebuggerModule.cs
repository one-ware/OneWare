using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Debugger.Helpers;
using OneWare.Debugger.Models;
using OneWare.Debugger.ViewModels;
using OneWare.Debugger.ViewModels.Inspector;
using OneWare.Debugger.ViewModels.Main;
using OneWare.Essentials.Debugger.Interfaces;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Debugger;

public class DebuggerModule : OneWareModuleBase
{
    public const string GdbPathSetting = "Debugger_GdbPath"; // Adresse des Stubs, an den sich GDB haengt. Leer heisst lokal debuggen.
    public const string RemoteEndpointSetting = "Debugger_RemoteEndpoint";
    public const string ExecutableProperty = "Debugger/Executable";

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IDebuggerService, DebuggerService>();
        services.AddSingleton<GdbSessionLauncher>();
        services.AddSingleton<DebuggerViewModel>();
        services.AddSingleton<MainPanelViewModel>();
        services.AddSingleton<RegisterTabViewModel>();
        services.AddSingleton<MemoryTabViewModel>();
        services.AddSingleton<ConsoleTabViewModel>();
        services.AddSingleton<VariablesViewModel>();
        services.AddSingleton<BreakpointsViewModel>();
        services.AddSingleton<InspectorViewModel>();

        // Einer fuer alle drei Wertetabellen -> Memory, Registers und Variables zeigen nie
        // verschiedene Zahlensysteme, und die Leiste ueber ihnen schaltet alle zugleich um.
        services.AddSingleton<ValueFormatViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var dockService = serviceProvider.Resolve<IMainDockService>();
        var settingsService = serviceProvider.Resolve<ISettingsService>();
        var paths = serviceProvider.Resolve<IPaths>();

        // Vor allem anderen: die Ansichtsmodelle lesen diese Werte im Konstruktor, und der
        // faellt spaetestens beim Registrieren der Panels weiter unten.
        // Register statt RegisterSetting -> gespeichert und damit ueber Neustarts hinweg
        // stabil, aber ohne Eintrag auf der Einstellungsseite: bedient wird die Anzeige in der
        // Leiste ueber der jeweiligen Tabelle.
        settingsService.Register(ValueFormatViewModel.BaseSetting, nameof(NumberBase.Hex));
        settingsService.Register(ValueFormatViewModel.SignedSetting, true);

        // Der GDB-Adapter ist das Backend des Kerns. Er deckt lokale Programme und, ueber
        // RemoteEndpoint, auch angehaengte Ziele ab; ein Plugin braucht nur dann einen eigenen
        // Adapter, wenn GDB sein Ziel gar nicht bedienen kann.
        serviceProvider.Resolve<IDebuggerService>().RegisterSessionLauncher<GdbSessionLauncher>();

        settingsService.RegisterSetting("Tools", "Debugger", GdbPathSetting,
            new FilePathSetting(
                "GDB Binary Path",
                GdbLocator.Find(paths.NativeToolsDirectory) ?? string.Empty,
                "No GDB found - Select Path or install GDB",
                paths.NativeToolsDirectory,
                PlatformHelper.ExistsOnPath,
                PlatformHelper.ExeFile)
            {
                HoverDescription = "Path to the GDB executable for debugging via gdbserver."
            });

        settingsService.RegisterSetting("Tools", "Debugger", RemoteEndpointSetting,
            new TextBoxSetting("Remote Endpoint", string.Empty, "host:port, e.g. localhost:1234")
            {
                HoverDescription = "Address of remote machine or debug stub to attach to. " +
                                   "Leave empty to debug the program on this machine."
            });
        
        dockService.RegisterLayoutExtension<DebuggerViewModel>(DockShowLocation.Bottom);
        dockService.RegisterLayoutExtension<InspectorViewModel>(DockShowLocation.RightPinned);
        
        serviceProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemModel("Debugger")
            {
                Header = "Debugger",
                Command = new RelayCommand(() =>
                {
                    dockService.Show(serviceProvider.Resolve<InspectorViewModel>(),
                        DockShowLocation.RightPinned);
                    dockService.Show(serviceProvider.Resolve<DebuggerViewModel>(), DockShowLocation.Bottom);
                }),
                Icon = new IconModel(DebuggerViewModel.IconKey)
            });
    }
}
