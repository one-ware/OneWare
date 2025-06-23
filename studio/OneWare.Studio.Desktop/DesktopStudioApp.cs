using System;
using System.IO;
using System.Linq;
using Autofac;
using Microsoft.Extensions.Logging;
using OneWare.Core.Services;
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

namespace OneWare.Studio.Desktop
{
    public class DesktopStudioApp
    {
        private readonly IContainer _container;
        private readonly ILogger<DesktopStudioApp> _logger;
        private readonly IPaths _paths;

        public DesktopStudioApp(IPluginService pluginService, ILogger<DesktopStudioApp> logger)
        {
            _logger = logger;
            var builder = new ContainerBuilder();

            // Register individual modules
            builder.RegisterModule<UpdaterModule>();
            builder.RegisterModule<PackageManagerModule>();
            builder.RegisterModule<TerminalManagerModule>();
            builder.RegisterModule<SourceControlModule>();
            builder.RegisterModule<SerialMonitorModule>();
            builder.RegisterModule<CppModule>();
            builder.RegisterModule<VhdlModule>();
            builder.RegisterModule<VerilogModule>();
            builder.RegisterModule<OssCadSuiteIntegrationModule>();

            // Register services
            builder.RegisterInstance(pluginService).As<IPluginService>();
            


            _container = builder.Build();
        }

        public void Configure()
        {
            try
            {
                var commandLineArgs = Environment.GetCommandLineArgs();
                if (commandLineArgs.Length > 1)
                {
                    var m = Array.FindIndex(commandLineArgs, x => x == "--modules");
                    if (m >= 0 && m < commandLineArgs.Length - 1)
                    {
                        var path = commandLineArgs[m + 1];
                        _container.Resolve<IPluginService>().AddPlugin(path);
                    }
                }

                var plugins = Directory.GetDirectories(_paths.PluginsDirectory);
                foreach (var module in plugins)
                {
                    _container.Resolve<IPluginService>().AddPlugin(module);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message, e);
            }
        }
    }
}
