using OneWare.Cpp;
using OneWare.Ghdl;
using OneWare.PackageManager;
using OneWare.SerialMonitor;
using OneWare.SourceControl;
using OneWare.TerminalManager;
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
        moduleCatalog.AddModule<TerminalManagerModule>();
        moduleCatalog.AddModule<SourceControlModule>();
        moduleCatalog.AddModule<SerialMonitorModule>();
        moduleCatalog.AddModule<CppModule>();
        moduleCatalog.AddModule<GhdlModule>();
    }
}