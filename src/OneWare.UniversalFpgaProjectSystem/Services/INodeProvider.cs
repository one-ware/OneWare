using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public interface INodeProvider
{
    public string Name { get; }

    public string[] SupportedLanguages { get; }

    public Task<IEnumerable<FpgaNode>> ExtractNodesAsync(IProjectFile file);

    /// <summary>
    /// Extracts top-level entity/module names from the given HDL file.
    /// The default implementation returns the filename without extension as a fallback.
    /// </summary>
    public Task<IEnumerable<string>> ExtractEntityNamesAsync(IProjectFile file)
    {
        return Task.FromResult<IEnumerable<string>>([Path.GetFileNameWithoutExtension(file.FullPath)]);
    }
}