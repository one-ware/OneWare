using OneWare.Shared.Models;

namespace OneWare.ProjectExplorer.Services;

public interface IFileWatchService
{
    public void Register(IFile file);
    public void Unregister(IFile file);
    
    public void Register(IProjectRoot project);
    
    public void Unregister(IProjectRoot project);
}