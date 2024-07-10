using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IProjectManagerService
{
    public void RegisterProjectManager(string id, IProjectManager manager);

    public IProjectManager? GetManager(string id);
}