namespace OneWare.Essentials.Models;

public interface IProjectRootWithFile : IProjectRoot
{
    public UniversalProjectProperties Properties { get; }
    public DateTime LastSaveTime { get; }
    public string ProjectFilePath { get; }
}