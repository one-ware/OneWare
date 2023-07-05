namespace OneWare.Shared;

public interface IProjectManager
{
    public Task<IProjectRoot?> LoadProjectAsync(string path);

    public Task<bool> SaveProjectAsync(IProjectRoot root);
}