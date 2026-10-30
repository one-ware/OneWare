using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneWare.CloudIntegration;
using OneWare.Core.ModuleLogic;
using OneWare.Core.Services;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;

namespace OneWare.Studio.Desktop.Cli;

internal sealed class CliModuleLoader(CliHostBuilderContext cliHostBuilder)
{
    private readonly Dictionary<string, Version> _pluginAssemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(Assembly Assembly, string PluginPath)> _skillAssemblies = [];
    private readonly List<(string Directory, string Id, string? Version, Func<IExternalAgentSkillProvider> Create)> _builtInSkillProviders = [];
    private readonly List<string> _pluginDiscoveryErrors = [];
    private bool _pluginDiscoveryComplete = true;

    public void RegisterBuiltInCliModules(Func<CancellationToken, Task<int>> stopStudio)
    {
        cliHostBuilder.ModuleCatalog.AddModule(new StudioCliModule(stopStudio));
        cliHostBuilder.ModuleCatalog.AddModule(new ExtensionStoreCliModule());
        cliHostBuilder.ModuleCatalog.AddModule(new OneWareCloudCliModule());
        cliHostBuilder.ModuleCatalog.AddModule(new AgentCliModule(this, cliHostBuilder.Paths));
        RegisterBuiltInSkillProvider<StudioExternalSkillProvider>();
    }

    public void RegisterBuiltInSkillProvider<T>() where T : IExternalAgentSkillProvider, new()
    {
        var directory = Path.GetDirectoryName(typeof(T).Assembly.Location)
                        ?? throw new InvalidOperationException($"Assembly for '{typeof(T).FullName}' has no directory.");
        var id = typeof(T).FullName
                 ?? throw new InvalidOperationException("The built-in skill provider has no type name.");
        _builtInSkillProviders.Add((directory, id, typeof(T).Assembly.GetName().Version?.ToString(), () => new T()));
    }

    public void LoadPluginCliModules()
    {
        // Plugins that extend another plugin are loaded after it
        foreach (var pluginPath in PluginAssemblyLoader.SortByDependencies(GetCliPluginDirectories()))
            try
            {
                if (!IsCompatiblePlugin(pluginPath))
                {
                    _pluginDiscoveryComplete = false;
                    continue;
                }

                var loadedModules = LoadCliModulesFromPath(pluginPath);
                foreach (var module in loadedModules)
                    cliHostBuilder.Logger.LogInformation(
                        "CLI module '{ModuleId}' loaded from '{PluginPath}'.",
                        module.Id,
                        pluginPath);
            }
            catch (Exception ex)
            {
                _pluginDiscoveryComplete = false;
                _pluginDiscoveryErrors.Add($"Could not load '{pluginPath}': {ex.Message}");
                cliHostBuilder.Logger.LogWarning(ex, "Failed loading CLI plugin modules from '{PluginPath}'.", pluginPath);
            }
    }

    public IReadOnlyList<ExternalSkillProviderSource> GetExternalSkillProviders()
    {
        if (!_pluginDiscoveryComplete)
            throw new InvalidOperationException("Some extensions could not be loaded; skill synchronization was cancelled. " +
                                                string.Join(" ", _pluginDiscoveryErrors));

        var providers = _builtInSkillProviders
            .Select(provider => new ExternalSkillProviderSource(
                provider.Directory, provider.Create(), provider.Id, provider.Version))
            .ToList();
        foreach (var (assembly, pluginPath) in _skillAssemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                throw new InvalidOperationException($"Could not inspect extension assembly '{assembly.Location}'.", ex);
            }

            foreach (var type in types.Where(type => type.IsPublic && !type.IsAbstract &&
                         typeof(IExternalAgentSkillProvider).IsAssignableFrom(type)))
            {
                if (type.GetConstructor(Type.EmptyTypes) is null)
                    throw new InvalidOperationException($"Skill provider '{type.FullName}' needs a public parameterless constructor.");
                IExternalAgentSkillProvider provider;
                try
                {
                    provider = (IExternalAgentSkillProvider)Activator.CreateInstance(type)!;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Could not create skill provider '{type.FullName}' from '{pluginPath}': " +
                        $"{ex.InnerException?.Message ?? ex.Message}", ex);
                }
                providers.Add(new ExternalSkillProviderSource(pluginPath, provider));
            }
        }

        return providers;
    }

    private bool IsCompatiblePlugin(string pluginPath)
    {
        var compatibilityFile = Path.Combine(pluginPath, "compatibility.txt");
        if (!File.Exists(compatibilityFile))
        {
            ReportIncompatiblePlugin(pluginPath, "compatibility.txt is missing.");
            return false;
        }

        var compatibility = PluginCompatibilityChecker.CheckCompatibilityPath(pluginPath, _pluginAssemblies);
        if (compatibility.IsCompatible)
            return true;

        var blockingIssues = compatibility.Issues
            .Where(issue => issue.Kind != CompatibilityRecordKind.MissingDependency)
            .ToArray();

        if (blockingIssues.Length == 0)
        {
            cliHostBuilder.Logger.LogInformation(
                "Loading CLI modules from '{PluginPath}' without unavailable GUI dependencies.",
                pluginPath);
            return true;
        }

        ReportIncompatiblePlugin(pluginPath, new CompatibilityReport(false, blockingIssues).Report);
        return false;
    }

    private void ReportIncompatiblePlugin(string pluginPath, string reason)
    {
        var message = $"Skipping incompatible CLI plugin '{pluginPath}': {reason}";
        _pluginDiscoveryErrors.Add(message);
        cliHostBuilder.Logger.LogWarning("{Message}", message);
    }

    private IEnumerable<string> GetCliPluginDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(cliHostBuilder.Paths.PluginsDirectory))
            foreach (var pluginDirectory in Directory.GetDirectories(cliHostBuilder.Paths.PluginsDirectory))
            {
                var fullPath = Path.GetFullPath(pluginDirectory);
                if (seen.Add(fullPath))
                    yield return fullPath;
            }

        var moduleValue = Environment.GetEnvironmentVariable("ONEWARE_MODULES");
        if (string.IsNullOrWhiteSpace(moduleValue))
            yield break;

        foreach (var modulePath in moduleValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fullPath = Path.GetFullPath(modulePath);
            if (!Directory.Exists(fullPath))
            {
                _pluginDiscoveryComplete = false;
                _pluginDiscoveryErrors.Add($"Module directory not found: '{fullPath}'.");
                cliHostBuilder.Logger.LogWarning("CLI module directory not found: '{PluginPath}'.", fullPath);
            }
            else if (seen.Add(fullPath))
                yield return fullPath;
        }
    }

    private IReadOnlyList<IOneWareCliModule> LoadCliModulesFromPath(string path)
    {
        return LoadCliModulesFromFiles(
            Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories)
                .Where(file => PluginAssemblyLoader.ShouldProbePluginAssembly(path, file)),
            path);
    }

    private IReadOnlyList<IOneWareCliModule> LoadCliModulesFromFiles(
        IEnumerable<string> assemblyFiles,
        string? pluginPath = null)
    {
        var assemblies = new List<Assembly>();
        var pluginAssemblies = new List<Assembly>();
        var addedAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var loadedAssembliesByName = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => new { Assembly = assembly, FullName = assembly.GetName().FullName })
            .Where(x => !string.IsNullOrWhiteSpace(x.FullName))
            .GroupBy(x => x.FullName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Assembly, StringComparer.OrdinalIgnoreCase);

        foreach (var file in assemblyFiles)
        {
            if (!PluginAssemblyLoader.TryGetManagedAssemblyName(file, out var assemblyName))
                continue;

            if (assemblyName.FullName is not { } fullName || !addedAssemblyNames.Add(fullName))
                continue;

            if (pluginPath != null && assemblyName is { Name: { } name, Version: { } version })
                _pluginAssemblies.TryAdd(name, version);

            if (loadedAssembliesByName.TryGetValue(fullName, out var loadedAssembly))
            {
                assemblies.Add(loadedAssembly);
                continue;
            }

            try
            {
                var assembly = Assembly.LoadFrom(file);
                assemblies.Add(assembly);
                pluginAssemblies.Add(assembly);
            }
            catch (Exception ex)
            {
                _pluginDiscoveryComplete = false;
                _pluginDiscoveryErrors.Add($"Could not load assembly '{file}': {ex.Message}");
                cliHostBuilder.Logger.LogWarning(ex, "Skipping CLI plugin assembly '{AssemblyFile}'.", Path.GetFileName(file));
            }
        }

        if (pluginPath is not null && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            PluginNativeLibraryResolver.Configure(
                pluginPath,
                pluginAssemblies,
                assembly => cliHostBuilder.Logger.LogWarning(
                    "Skipping resolver setup for {AssemblyName}, resolver already set.",
                    assembly.FullName));

        var added = new List<IOneWareCliModule>();
        foreach (var assembly in assemblies)
        {
            if (pluginPath is not null)
                _skillAssemblies.Add((assembly, pluginPath));
            added.AddRange(cliHostBuilder.ModuleCatalog.AddModulesFromAssembly(assembly));
        }

        return added;
    }
}
