using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneWare.Core.Models;
using OneWare.Core.ModuleLogic;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class PluginService : IPluginService, IPluginDependencyService
{
    private readonly IPaths _paths;
    private readonly OneWareModuleCatalog _moduleCatalog;
    private readonly OneWareModuleManager _moduleManager;
    private readonly ModuleServiceRegistry _moduleServiceRegistry;
    private readonly IApplicationStateService _applicationStateService;

    private readonly string _pluginDirectory;
    private readonly HashSet<string> _resolverSetAssemblies = new();
    // Removing files cannot unload assemblies, including assemblies from a partially failed activation.
    private readonly HashSet<string> _attemptedPluginLoads = new(StringComparer.OrdinalIgnoreCase);

    private List<Assembly> _initAssemblies;

    public PluginService(OneWareModuleCatalog moduleCatalog, OneWareModuleManager moduleManager,
        ModuleServiceRegistry moduleServiceRegistry, IPaths paths, IApplicationStateService applicationStateService)
    {
        _paths = paths;
        _moduleCatalog = moduleCatalog;
        _moduleManager = moduleManager;
        _moduleServiceRegistry = moduleServiceRegistry;
        _applicationStateService = applicationStateService;
        
        _initAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

        _pluginDirectory = Path.Combine(paths.SessionDirectory, "Plugins");
        Directory.CreateDirectory(_pluginDirectory);
    }

    public List<IPlugin> InstalledPlugins { get; } = new();

    public IPlugin AddPlugin(string path)
        => AddPlugin(path, new Dictionary<string, string>());

    public IPlugin AddPlugin(string path, IReadOnlyDictionary<string, string> dependencyPaths)
    {
        var id = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        if (InstalledPluginGraph.IsTransactionDirectoryName(id))
            throw new InvalidOperationException("Temporary package directories cannot be loaded as plugins.");
        if (_attemptedPluginLoads.Contains(id))
            throw new InvalidOperationException($"Plugin {id} has already been loaded in this process. Restart before reinstalling it.");
        // Update known assemblies to avoid redundant resolver registration
        _initAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

        var plugin = new Plugin(id, path);
        InstalledPlugins.Add(plugin);

        if (dependencyPaths.Keys.Any(id => !InstalledPlugins.Any(p => p.Id == id && p.IsCompatible)))
        {
            plugin.CompatibilityReport = "A declared plugin dependency failed to load. Restart or repair the dependency first.";
            return plugin;
        }

        if (PluginCompatibilityChecker.CheckCompatibilityPath(path,
            PluginCompatibilityChecker.ReadProvidedAssemblies(dependencyPaths.Values)) is { IsCompatible: false } test)
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
            _attemptedPluginLoads.Add(id);
            var realPath = Path.Combine(_pluginDirectory, id);
            PlatformHelper.CopyDirectory(path, realPath);

            var addedModules = LoadModulesFromPath(realPath);
            _moduleManager.RegisterPackageModules(plugin.Id, addedModules, dependencyPaths.Keys);

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
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) SetupNativeImports(realPath);
        }
        catch (Exception e)
        {
            plugin.IsCompatible = false;
            plugin.CompatibilityReport = e.Message;
            ContainerLocator.Container?.Resolve<ILogger>().Error(e.Message, e);
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
                     .Where(file => PluginCompatibilityChecker.ShouldProbePluginAssembly(path, file)))
        {
            if (!TryGetManagedAssemblyName(file, out var assemblyName))
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

    private static bool TryGetManagedAssemblyName(string filePath, out AssemblyName assemblyName)
    {
        try
        {
            assemblyName = AssemblyName.GetAssemblyName(filePath);
            return true;
        }
        catch (BadImageFormatException)
        {
        }
        catch (FileLoadException)
        {
        }

        assemblyName = null!;
        return false;
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
                    // Try 1 : Check runtimes folder
                    var libFileName = PlatformHelper.GetLibraryFileName(libraryName);
                    var libPath = Path.Combine(pluginPath, "runtimes", PlatformHelper.PlatformIdentifier, "native",
                        libFileName);
                    
                    // Try 2 : add lib infront in runtimes folder
                    if (!File.Exists(libPath))
                        libPath = Path.Combine(pluginPath, "runtimes", PlatformHelper.PlatformIdentifier, "native",
                            $"lib{libFileName}");

                    // Try 3: check base
                    if (!File.Exists(libPath)) 
                        libPath = Path.Combine(pluginPath, libFileName);

                    // Try 4 : base with lib infront
                    if (!File.Exists(libPath))
                        libPath = Path.Combine(pluginPath, $"lib{libFileName}");

                    // Try 5: MacOS weirdness, look in (own) base folder
                    // TODO find out why this is not automatic in MacOS, and why even without this we don't have issues
                    if (!File.Exists(libPath))
                        libPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, libFileName);

                    // Try 6: Same as 5 but added lib Prefix
                    if (!File.Exists(libPath))
                        libPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"lib{libFileName}");
                    
                    if (NativeLibrary.TryLoad(libPath, out var customHandle)) return customHandle;

                    if (NativeLibrary.TryLoad(libraryName, out var handle)) return handle;

                    Console.WriteLine($"Loading native library {libraryName} failed {File.Exists(libPath)}");
                    return IntPtr.Zero;
                });

                _resolverSetAssemblies.Add(assembly.FullName);
            }
            catch (InvalidOperationException)
            {
                // This assembly already has a resolver — log and continue
                ContainerLocator.Container.Resolve<ILogger>().Warning(
                    $"Skipping resolver setup for {assembly.FullName}, resolver already set.");
            }
        }
    }
}
