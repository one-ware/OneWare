using OneWare.Core;
using OneWare.Core.Services;
using OneWare.Shared.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Demo;

public class DemoApp : App
{
    public static readonly IPaths Paths = new Paths("OneWare Studio", "avares://OneWare.Demo/Assets/icon.ico", 
        "avares://OneWare.Demo/Assets/Startup.jpg");

    public static readonly ILogger Logger = new Logger(Paths);

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterInstance(Paths);
        containerRegistry.RegisterInstance(Logger);
        containerRegistry.RegisterInstance(SettingsService);
        base.RegisterTypes(containerRegistry);
    }

    protected override IModuleCatalog CreateModuleCatalog()
    {
        return new DirectoryModuleCatalog()
        {
            ModulePath = Paths.ModulesPath
        };
    }
}