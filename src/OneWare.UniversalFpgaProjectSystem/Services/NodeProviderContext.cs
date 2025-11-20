using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public class NodeProviderContext(INodeProviderRegistry nodeProviderRegistry): INodeProviderContext
{
    
    public Task<IEnumerable<FpgaNode>> ExtractNodesAsync(LanguageType type, IProjectFile file)
    {
        return nodeProviderRegistry.GetNodeProvider(type).ExtractNodesAsync(file);
    }
}