namespace OneWare.Shared;

public interface IProjectRoot : IProjectFolder
{
    List<IProjectFile> Files { get; }
    public string RootFolderPath { get; }
    public string? ProjectFileName { get; }
    public DateTime LastSaveTime { get; set; }
    public IProjectEntry? Search(string path, bool recursive = true);
}