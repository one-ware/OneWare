using OneWare.Essentials.Models;

namespace OneWare.ProjectExplorer.Services;

public interface IFileWatchService
{
    public void RegisterSingleFile(string file);
    
    public void UnregisterSingleFile(string file);

    public void RegisterProject(IProjectRoot project);

    public void UnregisterProject(IProjectRoot project);
}