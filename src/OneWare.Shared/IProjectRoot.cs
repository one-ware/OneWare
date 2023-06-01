namespace OneWare.Shared;

public interface IProjectRoot : IProjectFolder
{
    List<IProjectFile> Files { get; }
    public string RootFolderPath { get; }
    public DateTime LastSaveTime { get; set; }
}