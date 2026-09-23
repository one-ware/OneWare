using System.CommandLine;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OneWare.Core.ModuleLogic;
using OneWare.Core.Services;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;

internal sealed class CliModuleLoader(CliHostBuilderContext cliHostBuilder)
{
    public void RegisterBuiltInCliModules(
        Func<ParseResult, string?, bool, CancellationToken, Task<int>> startStudio,
        Func<CancellationToken, Task<int>> stopStudio)
    {
        cliHostBuilder.ModuleCatalog.AddModule(new StudioCliModule(startStudio, stopStudio));
    }

    public void LoadBundledCliModules()
    {
        var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
        var bundledAssemblyPaths = Directory
            .GetFiles(AppContext.BaseDirectory, "OneWare*.dll", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(
                Path.GetFullPath(path),
                entryAssemblyPath,
                StringComparison.OrdinalIgnoreCase));

        var loadedModules = LoadCliModulesFromFiles(bundledAssemblyPaths);
        foreach (var module in loadedModules)
            cliHostBuilder.Logger.LogInformation("Bundled CLI module '{ModuleId}' loaded.", module.Id);
    }

    public void LoadPluginCliModules()
    {
        foreach (var pluginPath in GetCliPluginDirectories())
            try
            {
                if (!IsCompatiblePlugin(pluginPath))
                    continue;

                var loadedModules = LoadCliModulesFromPath(pluginPath);
                foreach (var module in loadedModules)
                    cliHostBuilder.Logger.LogInformation(
                        "CLI module '{ModuleId}' loaded from '{PluginPath}'.",
                        module.Id,
                        pluginPath);
            }
            catch (Exception ex)
            {
                cliHostBuilder.Logger.LogWarning(ex, "Failed loading CLI plugin modules from '{PluginPath}'.", pluginPath);
            }
    }

    private bool IsCompatiblePlugin(string pluginPath)
    {
        var compatibilityFile = Path.Combine(pluginPath, "compatibility.txt");
        if (!File.Exists(compatibilityFile))
        {
            ReportIncompatiblePlugin(pluginPath, "compatibility.txt is missing.");
            return false;
        }

        var compatibility = PluginCompatibilityChecker.CheckCompatibilityPath(pluginPath);
        if (compatibility.IsCompatible)
            return true;

        ReportIncompatiblePlugin(pluginPath, compatibility.Report);
        return false;
    }

    private void ReportIncompatiblePlugin(string pluginPath, string reason)
    {
        var message = $"Skipping incompatible CLI plugin '{pluginPath}': {reason}";
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
            if (Directory.Exists(fullPath) && seen.Add(fullPath))
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
            .ToDictionary(x => x.FullName!, x => x.Assembly, StringComparer.OrdinalIgnoreCase);

        foreach (var file in assemblyFiles)
        {
            if (!PluginAssemblyLoader.TryGetManagedAssemblyName(file, out var assemblyName))
                continue;

            if (assemblyName.FullName is not { } fullName || !addedAssemblyNames.Add(fullName))
                continue;

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
            added.AddRange(cliHostBuilder.ModuleCatalog.AddModulesFromAssembly(assembly));

        return added;
    }
}
