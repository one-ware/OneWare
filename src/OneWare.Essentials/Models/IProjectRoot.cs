namespace OneWare.Essentials.Models;

public interface IProjectRoot : IProjectFolder
{
    public string ProjectTypeId { get; }
    public string ProjectPath { get; }
    public string RootFolderPath { get; }
    List<IProjectFile> Files { get; }
    public bool IsActive { get; set; }
    public bool IsPathIncluded(string relativePath);
    public void IncludePath(string path);
    public void OnExternalEntryAdded(string path, FileAttributes attributes);
}