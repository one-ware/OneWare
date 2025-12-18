using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public interface INodeProvider
{
    public string Name { get; }
    
    public string[] SupportedLanguages { get; }
    
    public Task<IEnumerable<FpgaNode>> ExtractNodesAsync(IProjectFile file);
}