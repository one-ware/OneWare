using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using OneWare.Core.Models;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using Autofac;
using Prism.Modularity;

namespace OneWare.Core.Services
{
    public class PluginService : IPluginService
    {
        private readonly IModuleCatalog _moduleCatalog;
        private readonly IModuleManager _moduleManager;
        private readonly ILogger _logger;

        private readonly string _pluginDirectory;
        private readonly HashSet<string> _resolverSetAssemblies = new();

        private List<Assembly> _initAssemblies;

        public PluginService(IModuleCatalog moduleCatalog, IModuleManager moduleManager, IPaths paths, ILogger logger)
        {
            _moduleCatalog = moduleCatalog;
            _moduleManager = moduleManager;
            _logger = logger;

            _initAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();
            _pluginDirectory = Path.Combine(paths.SessionDirectory, "Plugins");
            Directory.CreateDirectory(_pluginDirectory);
        }

        public List<IPlugin> InstalledPlugins { get; } = new();

        public IPlugin AddPlugin(string path)
        {
            // Update known assemblies to avoid redundant resolver registration
            _initAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

            var plugin = new Plugin(Path.GetFileName(path), path);
            InstalledPlugins.Add(plugin);

            if (PluginCompatibilityChecker.CheckCompatibilityPath(path) is { IsCompatible: false } test)
            {
                plugin.CompatibilityReport = test.Report;
                _logger.Error($"Plugin {path} failed loading: \n {test.Report}");
                return plugin;
            }

            plugin.IsCompatible = true;

            try
            {
                var realPath = Path.Combine(_pluginDirectory, Path.GetFileName(path));
                PlatformHelper.CopyDirectory(path, realPath);

                var catalog = new DirectoryModuleCatalog
                {
                    ModulePath = realPath
                };
                catalog.Initialize();

                foreach (var module in catalog.Modules)
                {
                    _moduleCatalog.AddModule(module);
                    if (_moduleCatalog.Modules.FirstOrDefault()?.State == ModuleState.Initialized)
                        _moduleManager.LoadModule(module.ModuleName);

                    _logger.Log($"Module {module.ModuleName} loaded", ConsoleColor.Cyan, true);
                }

                SetupNativeImports(realPath);
            }
            catch (Exception e)
            {
                _logger.Error(e.Message, e);
            }

            return plugin;
        }

        public void RemovePlugin(IPlugin plugin)
        {
            try
            {
                if (Directory.Exists(plugin.Path)) Directory.Delete(plugin.Path, true);
                InstalledPlugins.Remove(plugin);
            }
            catch (Exception e)
            {
                _logger.Error(e.Message, e);
            }
        }

        private void SetupNativeImports(string pluginPath)
        {
            var newAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(x => !_initAssemblies.Contains(x));

            foreach (var assembly in newAssemblies)
            {
                _initAssemblies.Add(assembly);

                if (assembly.FullName == null) continue;

                if (_resolverSetAssemblies.Contains(assembly.FullName))
                    continue;

                try
                {
                    NativeLibrary.SetDllImportResolver(assembly, (libraryName, _, _) =>
                    {
                        var libFileName = PlatformHelper.GetLibraryFileName(libraryName);
                        var libPath = Path.Combine(pluginPath, libFileName);

                        if (!File.Exists(libPath))
                        {
                            libPath = Path.Combine(pluginPath, "runtimes", PlatformHelper.PlatformIdentifier, "native", libFileName);
                        }

                        if (NativeLibrary.TryLoad(libPath, out var customHandle))
                        {
                            return customHandle;
                        }

                        if (NativeLibrary.TryLoad(libraryName, out var handle))
                        {
                            return handle;
                        }

                        Console.WriteLine($"Loading native library {libPath} failed");
                        return IntPtr.Zero;
                    });

                    _resolverSetAssemblies.Add(assembly.FullName);
                }
                catch (InvalidOperationException)
                {
                    // This assembly already has a resolver — log and continue
                    _logger.Log(
                        $"Skipping resolver setup for {assembly.FullName}, resolver already set.",
                        ConsoleColor.DarkYellow, true);
                }
            }
        }
    }
}
