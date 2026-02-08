namespace OneWare.Essentials.Models;

public interface IProjectManager
{
    public string Extension { get; }

    public Task<IProjectRoot?> LoadProjectAsync(string path);

    public Task ReloadProjectAsync(IProjectRoot project);

    public Task<bool> SaveProjectAsync(IProjectRoot root);
}