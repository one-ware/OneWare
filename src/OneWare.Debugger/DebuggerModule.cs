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
    public const string GdbPathSetting = "Debugger_GdbPath";

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<DebuggerViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var dockService = serviceProvider.Resolve<IMainDockService>();
        var settingsService = serviceProvider.Resolve<ISettingsService>();
        var paths = serviceProvider.Resolve<IPaths>();
        
        var gdbPath = GdbDebugAdapter.SuggestGdbPath();

        settingsService.RegisterSetting("Tools", "Debugger", GdbPathSetting,
            new FilePathSetting(
                "GDB Binary Path",
                gdbPath,
                "Auto-detected",
                paths.NativeToolsDirectory,
                PlatformHelper.ExistsOnPath,
                PlatformHelper.ExeFile)
            {
                HoverDescription = "Path to the GDB executable. Defaults to the auto-detected installation."
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
}