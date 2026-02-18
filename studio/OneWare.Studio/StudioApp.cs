using Avalonia.Markup.Xaml.Styling;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Core;
using OneWare.Core.Data;
using OneWare.Core.ModuleLogic;
using OneWare.Core.Services;
using OneWare.CruviAdapterExtensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Settings;
using OneWare.Studio.Styles;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.Vcd.Viewer;

namespace OneWare.Studio;

public class StudioApp : App
{
    public static readonly IProjectSettingsService ProjectSettingsService = new ProjectSettingsService();

    public static readonly ISettingsService SettingsService = new SettingsService();

    public static readonly IPaths Paths = new Paths("OneWare Studio", "avares://OneWare.Studio/Assets/icon.ico");

    static StudioApp()
    {
        SettingsService.Register("LastVersion", Global.VersionCode);
        SettingsService.RegisterSettingCategory("Experimental", 100, "MaterialDesign.Build");
        SettingsService.RegisterSetting("Experimental", "Misc", "Experimental_UseManagedFileDialog",
            new CheckBoxSetting("Use Managed File Dialog (restart required)", false)
            {
                HoverDescription =
                    "On some linux distros, the default file dialog is not available or will crash the app. Use this option to fix this issue. Restart required to apply this setting!"
            });
        SettingsService.RegisterSetting("Experimental", "Misc", "Experimental_AutoDownloadBinaries",
            new CheckBoxSetting("Automatically download Binaries", true)
            {
                HoverDescription = "Automatically download binaries for features when possible"
            });
        SettingsService.Load(Paths.SettingsPath);
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton(SettingsService);
        services.AddSingleton(ProjectSettingsService);
        services.AddSingleton(Paths);
        base.RegisterServices(services);
    }

    protected override string GetLogFilePath()
    {
        return Path.Combine(Paths.DocumentsDirectory, "Logs");
    }

    public override void Initialize()
    {
        var themeManager = new ThemeManager(SettingsService, Paths);
        base.Initialize();
        
        Styles.Add(new StudioStyles());
    }

    protected override void ConfigureModuleCatalog(OneWareModuleCatalog moduleCatalog)
    {
        base.ConfigureModuleCatalog(moduleCatalog);
        moduleCatalog.AddModule<UniversalFpgaProjectSystemModule>();
        moduleCatalog.AddModule<VcdViewerModule>();
        moduleCatalog.AddModule<CruviAdapterExtensionsModule>();
    }
}