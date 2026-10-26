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
using OneWare.Essentials.ProjectActions;
using OneWare.Essentials.Services;

namespace OneWare.Debugger;

public class DebuggerModule : OneWareModuleBase
{
    // Address of the stub GDB attaches to. Empty means debugging locally.
    public const string GdbPathSetting = "Debugger_GdbPath"; 
    public const string RemoteEndpointSetting = "Debugger_RemoteEndpoint";
    public const string RemoteProtocolLogSetting = "Debugger_LogRemoteProtocol";
    public const string ExecutableProperty = "Debugger/Executable";

    public static string RemoteProtocolLogCommand(bool enabled)
    {
        return $"set debug remote {(enabled ? 1 : 0)}";
    }

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IDebuggerService, DebuggerService>();
        services.AddSingleton<GdbSessionLauncher>();
        services.AddSingleton<DebuggerProjectActionProvider>();
        services.AddSingleton<DebuggerHoverProvider>();
        services.AddSingleton<DebuggerViewModel>();
        services.AddSingleton<MainPanelViewModel>();
        services.AddSingleton<RegisterTabViewModel>();
        services.AddSingleton<MemoryTabViewModel>();
        services.AddSingleton<ConsoleTabViewModel>();
        services.AddSingleton<VariablesViewModel>();
        services.AddSingleton<BreakpointsViewModel>();

        // One for all three value tables -> Memory, Registers and Variables never show
        // different number bases, and the Base selector in the toolbar switches all of them at once.
        services.AddSingleton<ValueFormatViewModel>();
        services.AddSingleton<InspectorEditSession>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var dockService = serviceProvider.Resolve<IMainDockService>();
        var settingsService = serviceProvider.Resolve<ISettingsService>();
        var paths = serviceProvider.Resolve<IPaths>();

        // Before anything else: the view models read these values in their constructor, which
        // runs at the latest when the panels are registered further down.
        // Register instead of RegisterSetting -> stored and therefore stable across restarts,
        // but without an entry on the settings page: the display is operated from the Base
        // selector in the debugger toolbar.
        settingsService.Register(ValueFormatViewModel.BaseSetting, nameof(NumberBase.Hex));

        // The GDB launcher is the core's backend. It covers local programs and, through
        // RemoteEndpoint, attached targets as well; a plugin only needs its own launcher
        // if GDB cannot serve its target at all.
        serviceProvider.Resolve<IDebuggerService>().RegisterSessionLauncher<GdbSessionLauncher>();
        serviceProvider.Resolve<IProjectActionService>().RegisterProvider<DebuggerProjectActionProvider>();
        serviceProvider.Resolve<IEditorHoverService>().RegisterProvider(serviceProvider.Resolve<DebuggerHoverProvider>());

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

        settingsService.RegisterSetting("Tools", "Debugger", RemoteProtocolLogSetting,
            new ToggleSwitchSetting("Log GDB Remote Protocol", false)
            {
                HoverDescription = "Logs every packet exchanged with the debug stub in the debugger console. " +
                                   "Takes effect immediately, also while a session is running."
            });

        settingsService.GetSettingObservable<bool>(RemoteProtocolLogSetting)
            .Subscribe(enabled => _ = serviceProvider.Resolve<IDebuggerService>()
                .SendRawCommandAsync(RemoteProtocolLogCommand(enabled)));

        settingsService.RegisterSetting("Tools", "Debugger", RemoteEndpointSetting,
            new TextBoxSetting("Remote Endpoint", string.Empty, "host:port, e.g. localhost:1234")
            {
                HoverDescription = "Address of remote machine or debug stub to attach to. " +
                                   "Leave empty to debug the program on this machine."
            });

        dockService.RegisterLayoutExtension<DebuggerViewModel>(DockShowLocation.Bottom);
        
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


