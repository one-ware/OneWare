using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Debugger.Helpers;
using OneWare.Debugger.ViewModels;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Debugger;

public class DebuggerModule : OneWareModuleBase
{
    public const string GdbPathSetting = "FEntwumS_Debugger_GdbPath";

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<DebuggerViewModel>();
        services.AddSingleton<DebuggerVariablesViewModel>();
        services.AddSingleton<DebuggerExpressionsViewModel>();
        services.AddSingleton<DebuggerBreakpointsViewModel>();
        services.AddSingleton<DebuggerInspectorViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var dockService = serviceProvider.Resolve<IMainDockService>();
        var settingsService = serviceProvider.Resolve<ISettingsService>();
        var paths = serviceProvider.Resolve<IPaths>();

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

        // Beide Panels gehoeren ins Standardlayout, sonst kaeme nach "Reset Layout" nur das
        // untere zurueck. Rechts genau ein angepinntes Dockable - dieselbe Konfiguration wie
        // beim AI-Chat, der einzigen, die stabil laeuft.
        dockService.RegisterLayoutExtension<DebuggerViewModel>(DockShowLocation.Bottom);
        dockService.RegisterLayoutExtension<DebuggerInspectorViewModel>(DockShowLocation.RightPinned);

        // Ein einziger Menuepunkt oeffnet die komplette Debugging-Ansicht: Steuerung samt
        // Console, Registers und Debugger Console unten, Variables, Expressions und Breakpoints
        // als Reiter im rechten Panel.
        serviceProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemModel("Debugging")
            {
                Header = "Debugging",
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