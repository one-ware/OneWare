using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneWare.Core.Models;
using OneWare.Core.ModuleLogic;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class PluginService : IPluginService
{
    private readonly IPaths _paths;
    private readonly OneWareModuleCatalog _moduleCatalog;
    private readonly OneWareModuleManager _moduleManager;
    private readonly ModuleServiceRegistry _moduleServiceRegistry;
    private readonly IApplicationStateService _applicationStateService;

    private readonly string _pluginDirectory;
    public PluginService(OneWareModuleCatalog moduleCatalog, OneWareModuleManager moduleManager,
        ModuleServiceRegistry moduleServiceRegistry, IPaths paths, IApplicationStateService applicationStateService)
    {
        _paths = paths;
        _moduleCatalog = moduleCatalog;
        _moduleManager = moduleManager;
        _moduleServiceRegistry = moduleServiceRegistry;
        _applicationStateService = applicationStateService;
        
        _pluginDirectory = Path.Combine(paths.SessionDirectory, "Plugins");
        Directory.CreateDirectory(_pluginDirectory);
    }

    public List<IPlugin> InstalledPlugins { get; } = new();

    public IPlugin AddPlugin(string path)
    {
        var initialAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToHashSet();

        var plugin = new Plugin(Path.GetFileName(path), path);
        InstalledPlugins.Add(plugin);

        if (PluginCompatibilityChecker.CheckCompatibilityPath(path) is { IsCompatible: false } test)
        {
            plugin.CompatibilityReport = test.Report;
            ContainerLocator.Container?.Resolve<ILogger>().Error($"Plugin {path} failed loading:\n{test.Report}", null, false);
            _applicationStateService.AddNotification(new ApplicationNotification()
            {
                Kind = ApplicationNotificationKind.Error,
                Message = $"Plugin {Path.GetFileName(path)} is not compatible with your version of OneWare."
            });
            return plugin;
        }

        plugin.IsCompatible = true;

        try
        {
            var realPath = Path.Combine(_pluginDirectory, Path.GetFileName(path));
            PlatformHelper.CopyDirectory(path, realPath);

            var addedModules = LoadModulesFromPath(realPath);

            if (addedModules.Count > 0 && ContainerLocator.Container != null)
            {
                var pluginServices = new ServiceCollection();
                _moduleManager.RegisterModuleServices(pluginServices, addedModules);
                _moduleServiceRegistry.AddDescriptors(pluginServices);
                if (_moduleManager.InitializationCompleted)
                    _moduleManager.InitializeModules(ContainerLocator.Current, addedModules);
            }

            //We should not use that anymore, since it can break compatibility with code signed apps
            //We keep it for now except on MacOS
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                PluginNativeLibraryResolver.Configure(
                    realPath,
                    AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !initialAssemblies.Contains(assembly)),
                    assembly => ContainerLocator.Container?.Resolve<ILogger>().Warning(
                        $"Skipping resolver setup for {assembly.FullName}, resolver already set."));
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
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
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }

    private IReadOnlyList<IOneWareModule> LoadModulesFromPath(string path)
    {
        var assemblies = new List<Assembly>();
        var loadedAssemblyNames = AppDomain.CurrentDomain.GetAssemblies()
            .Select(static assembly => assembly.GetName().FullName)
            .Where(static fullName => !string.IsNullOrWhiteSpace(fullName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories)
                     .Where(file => PluginAssemblyLoader.ShouldProbePluginAssembly(path, file)))
        {
            if (!PluginAssemblyLoader.TryGetManagedAssemblyName(file, out var assemblyName))
                continue;

            if (assemblyName.FullName is { } fullName && loadedAssemblyNames.Contains(fullName))
                continue;

            try
            {
                assemblies.Add(Assembly.LoadFrom(file));
                if (assemblyName.FullName is { } loadedFullName)
                    loadedAssemblyNames.Add(loadedFullName);
            }
            catch (Exception ex)
            {
                ContainerLocator.Container?.Resolve<ILogger>()
                    .Warning($"Skipping plugin assembly '{Path.GetFileName(file)}': {ex.Message}", ex);
            }
        }

        var added = new List<IOneWareModule>();
        foreach (var assembly in assemblies) added.AddRange(_moduleCatalog.AddModulesFromAssembly(assembly));

        foreach (var module in added)
            ContainerLocator.Container?.Resolve<ILogger>()
                .Log($"Module '{module.Id}' loaded");

        return added;
    }

}
