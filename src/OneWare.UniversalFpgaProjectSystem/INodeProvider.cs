using OneWare.Shared.Models;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem;

public interface INodeProvider
{
    public IEnumerable<NodeModel> ExtractNodes(IProjectFile file);
}