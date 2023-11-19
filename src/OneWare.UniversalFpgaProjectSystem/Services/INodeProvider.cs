using OneWare.Shared.Models;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public interface INodeProvider
{
    public IEnumerable<FpgaNode> ExtractNodes(IProjectFile file);
}