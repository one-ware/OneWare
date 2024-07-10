namespace OneWare.Essentials.Models;

public interface IProjectRootWithFile : IProjectRoot, ISavable
{
    public string ProjectFilePath { get; }
}