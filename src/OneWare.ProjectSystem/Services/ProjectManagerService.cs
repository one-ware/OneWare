using OneWare.Shared.Models;
using OneWare.Shared.Services;

namespace OneWare.ProjectSystem.Services;

public class ProjectManagerService : IProjectManagerService
{
    private readonly Dictionary<string, IProjectManager> _projectManagers = new();

    public void RegisterProjectManager(string id, IProjectManager manager)
    {
        _projectManagers.Add(id, manager);
    }
    
    public IProjectManager? GetManager(string id)
    {
        _projectManagers.TryGetValue(id, out var manager);
        //if (manager == null) throw new NullReferenceException($"Project Type {id} not registered!");
        return manager;
    }
}