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
    /// Returns the relative paths of files (included in the project) for this folder
    /// </summary>
    public IEnumerable<string> GetFiles(string searchPattern = "*", bool recursive = true);
    
    /// <summary>
    /// Returns the relative paths of directories (included in the project) for this folder
    /// </summary>
    public IEnumerable<string> GetDirectories(string searchPattern = "*", bool recursive = true);
}