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

    /// <summary>
    ///     Adds or replaces the tag with the given key, shown as a pill after the name in the project explorer
    /// </summary>
    public void AddTag(string key, ProjectExplorerTag tag);

    public void RemoveTag(string key);
}
