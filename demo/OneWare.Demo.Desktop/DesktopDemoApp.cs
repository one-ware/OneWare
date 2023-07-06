using OneWare.Cpp;
using OneWare.PackageManager;
using OneWare.SerialMonitor;
using OneWare.SourceControl;
using OneWare.Terminal;
using Prism.Modularity;

namespace OneWare.Demo.Desktop;

public class DesktopDemoApp : DemoApp
{
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
        moduleCatalog.AddModule<PackageManagerModule>();
        moduleCatalog.AddModule<TerminalModule>();
        moduleCatalog.AddModule<SourceControlModule>();
        moduleCatalog.AddModule<SerialMonitorModule>();
        moduleCatalog.AddModule<CppModule>();
    }
}