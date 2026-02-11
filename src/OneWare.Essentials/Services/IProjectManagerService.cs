using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IProjectManagerService
{
    /// <summary>
    /// Registers a project manager by project type ID.
    /// </summary>
    public void RegisterProjectManager(string id, IProjectManager manager);

    /// <summary>
    /// Returns a project manager by project type ID.
    /// </summary>
    public IProjectManager? GetManager(string id);

    /// <summary>
    /// Returns a project manager by file extension.
    /// </summary>
    public IProjectManager? GetManagerByExtension(string extension);
}
