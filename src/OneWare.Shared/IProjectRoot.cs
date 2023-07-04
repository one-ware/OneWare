namespace OneWare.Shared;

public interface IProjectRoot : IProjectFolder
{
    List<IProjectFile> Files { get; }
    public string RootFolderPath { get; }
    public DateTime LastSaveTime { get; set; }
    public bool IsActive { get; set; }
    public void Cleanup();
}