using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Debugger.ViewModels;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger;

public class DebuggerModule : OneWareModuleBase
{
    public const string GdbPathSetting = "Debugger_GdbPath";

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IDebugAdapter, GdbDebugAdapter>();
        services.AddSingleton<IDebuggerService, DebuggerService>();
        services.AddSingleton<DebuggerViewModel>();
        services.AddSingleton<DebuggerLocalsViewModel>();
        services.AddSingleton<DebuggerCallStackViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var dockService = serviceProvider.Resolve<IMainDockService>();
        var settingsService = serviceProvider.Resolve<ISettingsService>();
        var windowService = serviceProvider.Resolve<IWindowService>();

        var defaultGdb = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "gdb.exe" : "gdb";

        settingsService.RegisterSetting("Tools", "Debugger", GdbPathSetting,
            new FilePathSetting("GDB Path", defaultGdb, defaultGdb,
                serviceProvider.Resolve<IPaths>().NativeToolsDirectory,
                PlatformHelper.ExistsOnPath, PlatformHelper.ExeFile)
            {
                HoverDescription = "Path to the GDB executable used by the debugger. Leave blank to auto-detect on PATH."
            });

        dockService.RegisterLayoutExtension<DebuggerViewModel>(DockShowLocation.Bottom);
        dockService.RegisterLayoutExtension<DebuggerLocalsViewModel>(DockShowLocation.Right);
        dockService.RegisterLayoutExtension<DebuggerCallStackViewModel>(DockShowLocation.Right);

        _ = serviceProvider.Resolve<IDebuggerService>();

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemModel("Debugger")
            {
                Header = "Debugger",
                Command = new RelayCommand(() =>
                    dockService.Show(serviceProvider.Resolve<DebuggerViewModel>(), DockShowLocation.Bottom)),
                Icon = new IconModel(DebuggerViewModel.IconKey)
            });

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemModel("DebuggerLocals")
            {
                Header = "Debugger Locals",
                Command = new RelayCommand(() =>
                    dockService.Show(serviceProvider.Resolve<DebuggerLocalsViewModel>(), DockShowLocation.Right)),
                Icon = new IconModel(DebuggerLocalsViewModel.IconKey)
            });

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemModel("DebuggerCallStack")
            {
                Header = "Debugger Call Stack",
                Command = new RelayCommand(() =>
                    dockService.Show(serviceProvider.Resolve<DebuggerCallStackViewModel>(), DockShowLocation.Right)),
                Icon = new IconModel(DebuggerCallStackViewModel.IconKey)
            });
    }
}
