using System.Runtime.InteropServices;
using Avalonia.Markup.Xaml.Styling;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Core;
using OneWare.Core.Data;
using OneWare.Core.ModuleLogic;
using OneWare.Core.Services;
using OneWare.CruviAdapterExtensions;
using OneWare.Debugger;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.MarkdownViewer;
using OneWare.Settings;
using OneWare.Studio.Styles;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.Vcd.Viewer;
using ReactiveUI;

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
        SettingsService.RegisterSetting("Experimental", "Environment", "Experimental_AutoDownloadBinaries",
            new CheckBoxSetting("Automatically download Binaries", true)
            {
                HoverDescription = "Automatically download binaries for features when possible"
            });
        SettingsService.RegisterSetting("Experimental", "Environment", "Experimental_MaxGpuResourceSizeBytes",
            new ComboBoxSetting("Max GPU Resource Cache Size (restart required)", "Default (~28 MB)", new object[]
            {
                "Default (~28 MB)",
                "128 MB",
                "256 MB",
                "512 MB",
                "1 GB"
            })
            {
                HoverDescription = "Sets the maximum GPU memory Skia can use for cached textures. Increase if you experience rendering glitches, decrease if the app uses too much VRAM. Restart required."
            });
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            SettingsService.RegisterSetting("Experimental", "Environment", "Experimental_UseManagedFileDialog",
                new CheckBoxSetting("Use Managed File Dialog (restart required)", false)
                {
                    HoverDescription =
                        "On some linux distros, the default file dialog is not available or will crash the app. Use this option to fix this issue. Restart required to apply this setting!"
                });
            SettingsService.RegisterSetting("Experimental", "Environment", "Experimental_X11RenderingMode",
                new ComboBoxSetting("X11 Rendering Mode (restart required)", "Default (Glx, Software)", new object[]
                {
                    "Default (Glx, Software)",
                    "EGL",
                    "Software",
                    "Vulkan"
                })
                {
                    HoverDescription = "Sets the X11 rendering backend. Use 'Software' if you experience GPU rendering issues on Linux. Restart required."
                });
        }
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
        Name = Paths.AppName;
        
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
        moduleCatalog.AddModule<MarkdownViewerModule>();
        moduleCatalog.AddModule<DebuggerModule>();
    }
}