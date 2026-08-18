using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Debugger.Helpers;
using OneWare.Debugger.ViewModels;
using OneWare.Essentials.Debugger.Interfaces;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Debugger;

public class DebuggerModule : OneWareModuleBase
{
    public const string GdbPathSetting = "FEntwumS_Debugger_GdbPath"; // Adresse des Stubs, an den sich GDB haengt. Leer heisst lokal debuggen.
    public const string RemoteEndpointSetting = "FEntwumS_Debugger_RemoteEndpoint";

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IDebuggerService, DebuggerService>();
        services.AddSingleton<GdbDebugAdapter>();
        services.AddSingleton<DebuggerViewModel>();
        services.AddSingleton<DebuggerVariablesViewModel>();
        services.AddSingleton<DebuggerBreakpointsViewModel>();
        services.AddSingleton<DebuggerInspectorViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var dockService = serviceProvider.Resolve<IMainDockService>();
        var settingsService = serviceProvider.Resolve<ISettingsService>();
        var paths = serviceProvider.Resolve<IPaths>();

        // Der GDB-Adapter ist das Backend des Kerns. Er deckt lokale Programme und, ueber
        // RemoteEndpoint, auch angehaengte Ziele ab; ein Plugin braucht nur dann einen eigenen
        // Adapter, wenn GDB sein Ziel gar nicht bedienen kann.
        serviceProvider.Resolve<IDebuggerService>().RegisterAdapter<GdbDebugAdapter>();

        settingsService.RegisterSetting("Tools", "Debugger", GdbPathSetting,
            new FilePathSetting(
                "GDB Binary Path",
                GdbLocator.Find(paths.NativeToolsDirectory) ?? string.Empty,
                "No GDB found - Select Path or install GDB",
                paths.NativeToolsDirectory,
                PlatformHelper.ExistsOnPath,
                PlatformHelper.ExeFile)
            {
                HoverDescription = "Path to the GDB executable for remote debugging via gdbserver."
            });

        settingsService.RegisterSetting("Tools", "Debugger", RemoteEndpointSetting,
            new TextBoxSetting("Remote Endpoint", string.Empty, "host:port, e.g. localhost:3333")
            {
                HoverDescription = "Address of the gdbserver or debug stub to attach to. " +
                                   "Leave empty to debug the program on this machine."
            });
        
        dockService.RegisterLayoutExtension<DebuggerViewModel>(DockShowLocation.Bottom);
        dockService.RegisterLayoutExtension<DebuggerInspectorViewModel>(DockShowLocation.RightPinned);
        
        serviceProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemModel("Debugger")
            {
                Header = "Debugger",
                Command = new RelayCommand(() =>
                {
                    dockService.Show(serviceProvider.Resolve<DebuggerInspectorViewModel>(),
                        DockShowLocation.RightPinned);
                    dockService.Show(serviceProvider.Resolve<DebuggerViewModel>(), DockShowLocation.Bottom);
                }),
                Icon = new IconModel(DebuggerViewModel.IconKey)
            });
    }
}
