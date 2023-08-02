namespace OneWare.Shared;

public interface IProjectRootWithFile : IProjectRoot
{
    public string ProjectFilePath { get; }
}