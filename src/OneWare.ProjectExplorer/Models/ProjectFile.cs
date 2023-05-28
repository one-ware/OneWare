using OneWare.Shared;

namespace OneWare.ProjectExplorer.Models;

public class ProjectFile : ProjectEntry, IProjectFile, IFile
{
    public ProjectFile(string path, ProjectFolder topFolder) : base(path, topFolder)
    {
        
    }

    public int Version { get; set; }
}