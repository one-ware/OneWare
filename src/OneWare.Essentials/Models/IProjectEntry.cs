using System.Collections.ObjectModel;

namespace OneWare.Essentials.Models;

/// <summary>
///     Can be a file or a folder
/// </summary>
public interface IProjectEntry : IProjectExplorerNode
{
    public string FullPath { get; }
    
    public string Name { get; set; }
    
    public bool LoadingFailed { get; set; }

    public string RelativePath { get; }

    public IProjectRoot Root { get; }

    public IProjectFolder? TopFolder { get; set; }
}
