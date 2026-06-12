using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class FpgaTopEntityResult
{
    public required string TopEntity { get; init; }
    
    public required IProjectFile File { get; init; }
    
    public required INodeProvider NodeProvider { get; init; }

    public override string ToString()
    {
        return TopEntity;
    }
}