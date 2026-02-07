namespace OneWare.Essentials.Models;

public interface IProjectFolder : IProjectEntry
{
    public void Add(IProjectEntry entry);

    public void Remove(IProjectEntry entry);

    public void SetIsExpanded(bool newValue);

    public IProjectFile AddFile(string relativePath, bool createNew = false);

    public IProjectFolder AddFolder(string relativePath, bool createNew = false);

    public IProjectEntry? SearchRelativePath(string path);

    public IProjectEntry? SearchFullPath(string path);
}