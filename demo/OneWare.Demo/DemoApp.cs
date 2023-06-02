using Avalonia.Markup.Xaml.Styling;
using OneWare.Core;
using OneWare.Core.Services;
using OneWare.Cpp;
using OneWare.Settings;
using OneWare.Shared.Services;
using OneWare.SourceControl;
using OneWare.Vhdl;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Demo;

public class DemoApp : App
{
    public static readonly ISettingsService SettingsService = new SettingsService();
    
    public static readonly IPaths Paths = new Paths("OneWare Studio", "avares://OneWare.Demo/Assets/icon.ico",
        "avares://OneWare.Demo/Assets/Startup.jpg");

    private static readonly ILogger Logger = new Logger(Paths);

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
        
        this.Styles.Add(new StyleInclude(new Uri("avares://OneWare.Demo"))
        {
            Source = new Uri("avares://OneWare.Demo/Styles/Theme.axaml")
        });
    }

    protected override IModuleCatalog CreateModuleCatalog()
    {
        return new DirectoryModuleCatalog()
        {
            ModulePath = Paths.ModulesPath
        };
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        base.ConfigureModuleCatalog(moduleCatalog);
        moduleCatalog.AddModule<VhdlModule>();
        moduleCatalog.AddModule<CppModule>();
        moduleCatalog.AddModule<SourceControlModule>();
    }
}