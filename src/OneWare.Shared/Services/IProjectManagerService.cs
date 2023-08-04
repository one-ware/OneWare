using OneWare.Shared.Models;

namespace OneWare.Shared.Services;

public interface IProjectManagerService
{
    public void RegisterProjectManager(string id, IProjectManager manager);

    public IProjectManager? GetManager(string id);
}