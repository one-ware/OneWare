namespace OneWare.Essentials.Models;

public interface IProjectFolder : IProjectEntry
{
    public void Add(IProjectEntry entry);

    public void Remove(IProjectEntry entry);

    public void SetIsExpanded(bool newValue);

    public IProjectFile AddFile(string relativePath, bool createNew = false);

    public IProjectFolder AddFolder(string relativePath, bool createNew = false);

    public IProjectEntry? GetLoadedEntry(string relativePath);
    
    public IProjectEntry? GetEntry(string? relativePath);
    
    public IProjectFile? GetFile(string? relativePath);
    
    public IProjectFile? GetFolder(string? relativePath);
    
    /// <summary>
    /// Returns the relative paths of items included in this root
    /// </summary>
    public IEnumerable<string> GetFiles(string searchPattern = "*");
}