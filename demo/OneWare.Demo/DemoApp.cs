using System.Runtime.InteropServices;
using Avalonia.Markup.Xaml.Styling;
using OneWare.Core;
using OneWare.Core.Adapters;
using OneWare.Core.Data;
using OneWare.Core.Services;
using OneWare.Essentials.Services;
using OneWare.Settings;
using Prism.Ioc;

namespace OneWare.Demo;

public class DemoApp : App
{
    public static readonly ISettingsService SettingsService = new SettingsService();

    public static readonly IPaths Paths = new Paths("OneWare Demo", "avares://OneWare.Demo/Assets/icon.ico");

    private static readonly ILogger Logger = new Logger(Paths);

    static DemoApp()
    {
        SettingsService.Register("LastVersion", Global.VersionCode);
        SettingsService.RegisterSettingCategory("Experimental", 100, "MaterialDesign.Build");
        SettingsService.RegisterTitled("Experimental", "Misc", "Experimental_UseManagedFileDialog",
            "Use Managed File Dialog",
            "", RuntimeInformation.IsOSPlatform(OSPlatform.Linux));
    }

    protected override void RegisterServices(IContainerAdapter container)
    {
        container.RegisterInstance(SettingsService);
        container.RegisterInstance(Paths);
        container.RegisterInstance(Logger);
    }

    public override void Initialize()
    {
        var themeManager = new ThemeManager(SettingsService, Paths);
        base.Initialize();

        Styles.Add(new StyleInclude(new Uri("avares://OneWare.Demo"))
        {
            Source = new Uri("avares://OneWare.Demo/Styles/Theme.axaml")
        });
    }
}