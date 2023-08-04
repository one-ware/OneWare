namespace OneWare.Shared.Models;

public interface IProjectRootWithFile : IProjectRoot, ISavable
{
    public string ProjectFilePath { get; }
}