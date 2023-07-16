using OneWare.Shared;

namespace OneWare.ProjectExplorer.Services;

public interface IFileWatchService
{
    public void Register(IProjectRoot project);
    
    public void Unregister(IProjectRoot project);
}