namespace OneWare.Essentials.Models;

public interface IProjectRoot : IProjectFolder
{
    public string ProjectTypeId { get; }
    public string ProjectPath { get; }
    public string RootFolderPath { get; }
    public bool IsActive { get; set; }
    public bool IsPathIncluded(string relativePath);
    public void IncludePath(string path);
    public void OnExternalEntryAdded(string path, FileAttributes attributes);
    public IProjectEntry? GetEntry(string relativePath);
    public IProjectFile? GetFile(string relativePath);
    
    /// <summary>
    /// Returns the relative paths of items included in this root
    /// </summary>
    public IEnumerable<string> GetFiles(string searchPattern = "*");
}