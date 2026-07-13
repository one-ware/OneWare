using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public interface INodeProvider
{
    public string Name { get; }

    public string[] SupportedLanguages { get; }

    /// <summary>
    /// Extracts all I/O nodes (ports) from the given file, across all entities/modules.
    /// </summary>
    public Task<IEnumerable<FpgaNode>> ExtractNodesAsync(IProjectFile file);

    /// <summary>
    /// Returns the names of all top-level entities or modules declared in the given file.
    /// Default implementation returns the file name without extension as a single entry.
    /// </summary>
    public Task<IEnumerable<string>> ExtractTopEntitiesAsync(IProjectFile file) =>
        Task.FromResult<IEnumerable<string>>([Path.GetFileNameWithoutExtension(file.FullPath)]);

    /// <summary>
    /// Extracts I/O nodes for a specific named entity or module within the file.
    /// Default implementation ignores <paramref name="topEntityName"/> and returns all nodes.
    /// </summary>
    public Task<IEnumerable<FpgaNode>> ExtractNodesAsync(IProjectFile file, string topEntityName) =>
        ExtractNodesAsync(file);
}