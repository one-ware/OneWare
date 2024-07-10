using System.Collections.ObjectModel;

namespace OneWare.Essentials.Models;

/// <summary>
/// Can be a file or a folder
/// </summary>
public interface IProjectEntry : IProjectExplorerNode, IHasPath
{
    public ObservableCollection<IProjectEntry> Entities { get; }
    
    public string RelativePath { get; }

    public IProjectRoot Root { get; }
    
    public IProjectFolder? TopFolder { get; set; }

    public Action<Action<string>>? RequestRename { get; set; }
    
    public bool IsValid();
}