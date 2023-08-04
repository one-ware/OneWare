namespace OneWare.Shared.Models;

public interface IProjectFolder : IProjectEntry
{
    public void Add(IProjectEntry entry);
    
    public void Remove(IProjectEntry entry);

    public IProjectFile AddFile(string path, bool createNew = false);

    public IProjectFolder AddFolder(string path, bool createNew = false);

    public IProjectEntry? Search(string path, bool recursive = true);
}