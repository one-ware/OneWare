namespace OneWare.Essentials.Models;

public interface IProjectRootWithFile : IProjectRoot
{
    public DateTime LastSaveTime { get; }
    public string ProjectFilePath { get; }
}