using OneWare.Essentials.Models;
using OneWare.ProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class FpgaProjectFile : ProjectFile
{
    public FpgaProjectFile(string header, IProjectFolder topFolder) : base(header, topFolder)
    {
    }
}