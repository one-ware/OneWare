using System.Collections.ObjectModel;

namespace OneWare.Shared;

public interface IProjectFolder : IProjectEntry
{
    public ObservableCollection<IProjectEntry> Items { get; }
    
    public bool IsExpanded { get; set; }

    public void Remove(IProjectEntry entry);

    public IProjectFile AddFile(string path, bool createNew = false);

    public IProjectFolder AddFolder(string path, bool createNew = false);
}