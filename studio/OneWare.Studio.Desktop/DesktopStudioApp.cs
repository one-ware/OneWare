using System;
using System.IO;
using ImTools;
using OneWare.Cpp;
using OneWare.Essentials.Services;
using OneWare.OssCadSuiteIntegration;
using OneWare.PackageManager;
using OneWare.SerialMonitor;
using OneWare.SourceControl;
using OneWare.TerminalManager;
using OneWare.Updater;
using OneWare.Verilog;
using OneWare.Vhdl;
using Prism.Modularity;

namespace OneWare.Studio.Desktop;

public class DesktopStudioApp : StudioApp
{
    private IContainerProvider Container { get; }

    public DesktopStudioApp(IContainerProvider containerProvider)
    {
        Container = containerProvider;
    }

    public DesktopStudioApp()
    {
        // Initialization logic if needed
    }

    // Existing methods remain unchanged
    public void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        base.ConfigureModuleCatalog(moduleCatalog);

        moduleCatalog.AddModule<UpdaterModule>();
        moduleCatalog.AddModule<PackageManagerModule>();
        moduleCatalog.AddModule<TerminalManagerModule>();
        moduleCatalog.AddModule<SourceControlModule>();
        moduleCatalog.AddModule<SerialMonitorModule>();
        moduleCatalog.AddModule<CppModule>();
        moduleCatalog.AddModule<VhdlModule>();
        moduleCatalog.AddModule<VerilogModule>();
        moduleCatalog.AddModule<OssCadSuiteIntegrationModule>();

        try
        {
            var commandLineArgs = Environment.GetCommandLineArgs();
            if (commandLineArgs.Length > 1)
            {
                var m = commandLineArgs.IndexOf(x => x == "--modules");
                if (m >= 0 && m < commandLineArgs.Length - 1)
                {
                    var path = commandLineArgs[m + 1];
                    Container.Resolve<IPluginService>().AddPlugin(path);
                }
            }

            var plugins = Directory.GetDirectories(Paths.PluginsDirectory);
            foreach (var module in plugins) Container.Resolve<IPluginService>().AddPlugin(module);
        }
        catch (Exception e)
        {
            Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }
}
