namespace OneWare.Essentials.Models;

public interface IProjectManager
{
    public Task<IProjectRoot?> LoadProjectAsync(string path);

    public Task<bool> SaveProjectAsync(IProjectRoot root);
}