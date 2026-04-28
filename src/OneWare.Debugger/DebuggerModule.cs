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
        services.AddSingleton<IDebuggerService, DebuggerService>();
        services.AddSingleton<DebuggerViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var dockService = serviceProvider.Resolve<IMainDockService>();

        // Cross-platform default for the GDB executable. Users can override
        // this in settings; if left at the default we look the binary up via
        // PATH at debug-start time.
        var defaultGdb = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "gdb.exe" : "gdb";

        serviceProvider.Resolve<ISettingsService>().RegisterSetting("Tools", "Debugger", GdbPathSetting,
            new FilePathSetting("GDB Path", defaultGdb, defaultGdb,
                serviceProvider.Resolve<IPaths>().NativeToolsDirectory,
                PlatformHelper.ExistsOnPath, PlatformHelper.ExeFile)
            {
                HoverDescription = "Path to the GDB executable used by the debugger. Leave blank to auto-detect on PATH."
            });

        // Eagerly resolve the debugger service so it starts observing the
        // shared breakpoint store right away.
        var debuggerService = serviceProvider.Resolve<IDebuggerService>();

        serviceProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemModel("Debugger")
            {
                Header = "Debugger",
                Command = new RelayCommand(() =>
                    dockService.Show(serviceProvider.Resolve<DebuggerViewModel>(), DockShowLocation.Bottom)),
                Icon = new IconModel(DebuggerViewModel.IconKey)
            });
    }
}