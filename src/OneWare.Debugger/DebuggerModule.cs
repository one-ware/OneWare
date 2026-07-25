using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
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
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var dockService = serviceProvider.Resolve<IMainDockService>();
        var settingsService = serviceProvider.Resolve<ISettingsService>();
        var paths = serviceProvider.Resolve<IPaths>();

        settingsService.RegisterSetting("Tools", "Debugger", GdbPathSetting,
            new FilePathSetting(
                "GDB Binary Path",
                SuggestGdbPath(),
                "Auto-detected",
                paths.NativeToolsDirectory,
                PlatformHelper.ExistsOnPath,
                PlatformHelper.ExeFile)
            {
                HoverDescription = "Pfad zur GDB-Executable für Remote-Debugging über gdbserver."
            });

        serviceProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemModel("Debugger")
            {
                Header = "Debugger",
                Command = new RelayCommand(() =>
                    dockService.Show(serviceProvider.Resolve<DebuggerViewModel>(), DockShowLocation.Bottom)),
                Icon = new IconModel(DebuggerViewModel.IconKey)
            });
    }

    private static string SuggestGdbPath()
    {
        // Plattformüblicher Standard-Installationsort zuerst prüfen.
        string? defaultLocation = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            defaultLocation = @"C:\Program Files\gdb\bin\gdb.exe";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            defaultLocation = "/opt/homebrew/bin/gdb";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            defaultLocation = "/usr/bin/gdb";

        if (defaultLocation != null && File.Exists(defaultLocation))
            return defaultLocation;

        // Kein GDB am Standardort -> im System-PATH suchen.
        var binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "gdb.exe" : "gdb";
        return PlatformHelper.GetFullPath(binaryName) ?? binaryName;
    }
}