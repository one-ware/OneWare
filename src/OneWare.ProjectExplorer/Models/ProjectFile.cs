using System.Runtime.Serialization;
using OneWare.Shared;

namespace OneWare.ProjectExplorer.Models;

[DataContract]
public class ProjectFile : ProjectEntry, IProjectFile
{
    public ProjectFile(string header, IProjectFolder topFolder) : base(header, topFolder)
    {
        
    }

    public int Version { get; set; }
}