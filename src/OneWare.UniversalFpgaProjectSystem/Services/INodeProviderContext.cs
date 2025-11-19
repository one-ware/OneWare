using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public interface INodeProviderContext
{
    public Task<IEnumerable<FpgaNode>> ExtractNodesAsync(string language, IProjectFile file);
}