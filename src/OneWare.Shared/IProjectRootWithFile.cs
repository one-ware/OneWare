namespace OneWare.Shared;

public interface IProjectRootWithFile : IProjectRoot, ISavable
{
    public string ProjectFilePath { get; }
}