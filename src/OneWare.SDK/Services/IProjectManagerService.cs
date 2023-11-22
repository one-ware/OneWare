using OneWare.SDK.Models;

namespace OneWare.SDK.Services;

public interface IProjectManagerService
{
    public void RegisterProjectManager(string id, IProjectManager manager);

    public IProjectManager? GetManager(string id);
}