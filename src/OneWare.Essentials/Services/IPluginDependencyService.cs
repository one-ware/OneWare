using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

/// <summary>Explicit declared package providers, not arbitrary assemblies loaded in the process.</summary>
public interface IPluginDependencyService
{
    IPlugin AddPlugin(string path, IReadOnlyDictionary<string, string> dependencyPaths);
}