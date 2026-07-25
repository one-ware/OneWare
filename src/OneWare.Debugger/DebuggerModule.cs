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

        // Legt die Standardposition der Panels im Layout fest. Rechts wird RightPinned genutzt,
        // weil das Standardlayout nur dafuer einen Bereich anlegt (DefaultLayout.cs) - genau wie
        // beim AI-Chat-Panel.
        dockService.RegisterLayoutExtension<DebuggerViewModel>(DockShowLocation.Bottom);
        dockService.RegisterLayoutExtension<DebuggerVariablesViewModel>(DockShowLocation.RightPinned);
        dockService.RegisterLayoutExtension<DebuggerExpressionsViewModel>(DockShowLocation.RightPinned);
        dockService.RegisterLayoutExtension<DebuggerBreakpointsViewModel>(DockShowLocation.RightPinned);

        // Ein einziger Menuepunkt oeffnet die komplette Debugging-Ansicht: Steuerung samt
        // Call Stack und Ausgaben unten, Variables, Expressions und Breakpoints rechts.
        serviceProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemModel("Debugging")
            {
                Header = "Debugging",
                Command = new RelayCommand(() =>
                {
                    dockService.Show(serviceProvider.Resolve<DebuggerVariablesViewModel>(),
                        DockShowLocation.RightPinned);
                    dockService.Show(serviceProvider.Resolve<DebuggerExpressionsViewModel>(),
                        DockShowLocation.RightPinned);
                    dockService.Show(serviceProvider.Resolve<DebuggerBreakpointsViewModel>(),
                        DockShowLocation.RightPinned);
                    dockService.Show(serviceProvider.Resolve<DebuggerViewModel>(), DockShowLocation.Bottom);
                }),
                Icon = new IconModel(DebuggerViewModel.IconKey)
            });
    }
}