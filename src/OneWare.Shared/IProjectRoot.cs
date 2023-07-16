namespace OneWare.Shared;

public interface IProjectRoot : IProjectFolder
{
    public string ProjectTypeId { get; }
    public string ProjectPath { get; }
    public string RootFolderPath { get; }
    List<IProjectFile> Files { get; }
    public DateTime LastSaveTime { get; set; }
    public bool IsActive { get; set; }
}