using System.Runtime.InteropServices;
using Avalonia.Markup.Xaml.Styling;
using OneWare.Core;
using OneWare.Core.Data;
using OneWare.Core.Services;
using OneWare.CruviAdapterExtensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Settings;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.Vcd.Viewer;
using Prism.Modularity;

namespace OneWare.Studio;

public class StudioApp : App
{
    public static readonly ISettingsService SettingsService = new SettingsService();

    public static readonly IProjectSettingsService ProjectSettingsService = new ProjectSettingsService();

    public static readonly IPaths Paths = new Paths("OneWare Studio", "avares://OneWare.Studio/Assets/icon.ico");

    private static readonly ILogger Logger = new Logger(Paths);

    static StudioApp()
    {
        SettingsService.Register("LastVersion", Global.VersionCode);
        SettingsService.RegisterSettingCategory("Experimental", 100, "MaterialDesign.Build");
        SettingsService.RegisterTitled("Experimental", "Misc", "Experimental_UseManagedFileDialog",
            "Use Managed File Dialog (restart required)",
            "On some linux distros, the default file dialog is not available or will crash the app. Use this option to fix this issue. Restart required to apply this setting!",
            false);
        SettingsService.RegisterTitled("Experimental", "Misc", "Experimental_AutoDownloadBinaries",
            "Automatically download Binaries",
            "Automatically download binaries for features when possible", true);
        SettingsService.Load(Paths.SettingsPath);
    }

    protected virtual void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterInstance(SettingsService);
        containerRegistry.RegisterInstance(ProjectSettingsService);
        containerRegistry.RegisterInstance(Paths);
        containerRegistry.RegisterInstance(Logger);

        // Removed the call to base.RegisterTypes(containerRegistry) as App does not define RegisterTypes.
    }

    public override void Initialize()
    {
        var themeManager = new ThemeManager(SettingsService, Paths);
        base.Initialize();

        Styles.Add(new StyleInclude(new Uri("avares://OneWare.Studio"))
        {
            Source = new Uri("avares://OneWare.Studio/Styles/Theme.axaml")
        });
    }

    protected void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {      
        moduleCatalog.AddModule<UniversalFpgaProjectSystemModule>();
        moduleCatalog.AddModule<VcdViewerModule>();
        moduleCatalog.AddModule<CruviAdapterExtensionsModule>();
        //moduleCatalog.AddModule<ChatBotModule>();
    }
}