using OneWare.Essentials.Models;
using OneWare.ProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class FpgaProjectFolder : ProjectFolder
{
    public FpgaProjectFolder(string header, IProjectFolder? topFolder) : base(header, topFolder)
    {
    }

    protected override IProjectFolder ConstructNewProjectFolder(string path, IProjectFolder topFolder)
    {
        return new FpgaProjectFolder(path, topFolder);
    }

    protected override IProjectFile ConstructNewProjectFile(string path, IProjectFolder topFolder)
    {
        return new FpgaProjectFile(path, topFolder);
    }
}