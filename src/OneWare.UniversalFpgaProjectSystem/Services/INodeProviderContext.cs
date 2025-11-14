using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public interface INodeProviderContext
{
    public IEnumerable<FpgaNode> ExtractNodes(IProjectFile file);
}