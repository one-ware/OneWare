using Autofac;
using Avalonia;
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
using System;

namespace OneWare.Studio;

public class StudioApp : App
{
    public static IContainer Container { get; private set; } = null!;

    public override void Initialize()
    {
        base.Initialize();
        var builder = new ContainerBuilder();

        RegisterInfrastructure(builder);
        RegisterModules(builder);

        Container = builder.Build();

        ApplyTheme(Container.Resolve<ThemeManager>());
    }

    private void RegisterInfrastructure(ContainerBuilder builder)
    {
        var settingsService = new SettingsService();
        var projectSettingsService = new ProjectSettingsService();
        var paths = new Paths("OneWare Studio", "avares://OneWare.Studio/Assets/icon.ico");
        var logger = new Logger(paths);

        settingsService.Register("LastVersion", Global.VersionCode);
        settingsService.RegisterSettingCategory("Experimental", 100, "MaterialDesign.Build");
        settingsService.RegisterTitled("Experimental", "Misc", "Experimental_UseManagedFileDialog",
            "Use Managed File Dialog (restart required)",
            "On some linux distros, the default file dialog is not available or will crash the app. Use this option to fix this issue. Restart required to apply this setting!",
            false);
        settingsService.RegisterTitled("Experimental", "Misc", "Experimental_AutoDownloadBinaries",
            "Automatically download Binaries",
            "Automatically download binaries for features when possible", true);
        settingsService.Load(paths.SettingsPath);

        // Register core infrastructure
        builder.RegisterInstance(settingsService).As<ISettingsService>().SingleInstance();
        builder.RegisterInstance(projectSettingsService).As<IProjectSettingsService>().SingleInstance();
        builder.RegisterInstance(paths).As<IPaths>().SingleInstance();
        builder.RegisterInstance(logger).As<ILogger>().SingleInstance();

        builder.RegisterType<ThemeManager>().AsSelf().SingleInstance();
    }

    private void RegisterModules(ContainerBuilder builder)
    {
        UniversalFpgaProjectSystemModule.Register(builder);
        VcdViewerModule.Register(builder);
        CruviAdapterExtensionsModule.Register(builder);
        // Add more modules as needed
    }

    private void ApplyTheme(ThemeManager themeManager)
    {
        Styles.Add(new StyleInclude(new Uri("avares://OneWare.Studio"))
        {
            Source = new Uri("avares://OneWare.Studio/Styles/Theme.axaml")
        });
    }
}
