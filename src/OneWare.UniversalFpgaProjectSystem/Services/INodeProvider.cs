using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public interface INodeProvider
{
    public Task<IEnumerable<FpgaNode>> ExtractNodesAsync(IProjectFile file);
    
    public string GetDisplayName();

    public string GetKey();
}