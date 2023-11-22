namespace OneWare.SDK.Models;

public interface IProjectRootWithFile : IProjectRoot, ISavable
{
    public string ProjectFilePath { get; }
}