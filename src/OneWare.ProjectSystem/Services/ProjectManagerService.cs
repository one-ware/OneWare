using OneWare.Shared;
using OneWare.Shared.Services;

namespace OneWare.ProjectSystem.Services;

public class ProjectManagerService : IProjectManagerService
{
    private readonly Dictionary<Type, IProjectManager> _projectManagers = new();

    public void RegisterProjectManager(Type type, IProjectManager manager)
    {
        _projectManagers.Add(type, manager);
    }
    
    public IProjectManager GetManager(Type type)
    {
        _projectManagers.TryGetValue(type, out var manager);
        if (manager == null) throw new NullReferenceException($"Project Type {type} not registered!");
        return manager;
    }
}