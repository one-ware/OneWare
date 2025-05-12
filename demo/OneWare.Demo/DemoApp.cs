using System.Runtime.InteropServices;
using Avalonia.Markup.Xaml.Styling;
using OneWare.Core;
using OneWare.Core.Data;
using OneWare.Core.Services;
using OneWare.Essentials.Services;
using OneWare.Settings;
using Autofac;

namespace OneWare.Demo;

public class DemoApp : App
{
    public static readonly ISettingsService SettingsService = new SettingsService();
    public static readonly IPaths Paths = new Paths("OneWare Demo", "avares://OneWare.Demo/Assets/icon.ico");
    private static readonly ILogger Logger = new Logger(Paths);

    public static new IContainer Container { get; private set; }

    static DemoApp()
    {
        SettingsService.Register("LastVersion", Global.VersionCode);
        SettingsService.RegisterSettingCategory("Experimental", 100, "MaterialDesign.Build");
        SettingsService.RegisterTitled("Experimental", "Misc", "Experimental_UseManagedFileDialog",
            "Use Managed File Dialog", "",
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux));
    }

    public override void Initialize()
    {
        base.Initialize();

        Styles.Add(new StyleInclude(new Uri("avares://OneWare.Demo"))
        {
            Source = new Uri("avares://OneWare.Demo/Styles/Theme.axaml")
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var builder = new ContainerBuilder();

        // Register core services
        builder.RegisterInstance(SettingsService).As<ISettingsService>();
        builder.RegisterInstance(Paths).As<IPaths>();
        builder.RegisterInstance(Logger).As<ILogger>();

        // Register additional services/viewmodels as needed...

        Container = builder.Build();

        base.OnFrameworkInitializationCompleted();
    }
}
