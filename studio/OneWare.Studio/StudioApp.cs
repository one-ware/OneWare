using System.Runtime.InteropServices;
using Avalonia.Markup.Xaml.Styling;
using OneWare.Core;
using OneWare.Core.Data;
using OneWare.Core.Services;
using OneWare.IceBreaker;
using OneWare.Json;
using OneWare.SDK.Services;
using OneWare.Settings;
using OneWare.Toml;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.Vcd.Viewer;
using OneWare.Verilog;
using OneWare.Vhdl;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Studio;

public class StudioApp : App
{
    public static readonly ISettingsService SettingsService = new SettingsService();
    
    public static readonly IPaths Paths = new Paths("OneWare Studio", "avares://OneWare.Studio/Assets/icon.ico");

    private static readonly ILogger Logger = new Logger(Paths);

    static StudioApp()
    {
        SettingsService.Register("LastVersion", Global.VersionCode);
        SettingsService.RegisterSettingCategory("Experimental", 100, "MaterialDesign.Build");
        SettingsService.RegisterTitled("Experimental", "Misc", "Experimental_UseManagedFileDialog", "Use Managed File Dialog",
            "", RuntimeInformation.IsOSPlatform(OSPlatform.Linux));
        SettingsService.Load(Paths.SettingsPath);
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterInstance(SettingsService);
        containerRegistry.RegisterInstance(Paths);
        containerRegistry.RegisterInstance(Logger);
        
        base.RegisterTypes(containerRegistry);
    }

    public override void Initialize()
    {
        var themeManager = new ThemeManager(SettingsService, Paths);
        base.Initialize();
        
        this.Styles.Add(new StyleInclude(new Uri("avares://OneWare.Studio"))
        {
            Source = new Uri("avares://OneWare.Studio/Styles/Theme.axaml")
        });
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        base.ConfigureModuleCatalog(moduleCatalog);
        moduleCatalog.AddModule<UniversalFpgaProjectSystemModule>();
        moduleCatalog.AddModule<VhdlModule>();
        moduleCatalog.AddModule<VerilogModule>();
        moduleCatalog.AddModule<JsonModule>();
        moduleCatalog.AddModule<TomlModule>();
        moduleCatalog.AddModule<VcdViewerModule>();
        moduleCatalog.AddModule<IceBreakerModule>();
    }
}