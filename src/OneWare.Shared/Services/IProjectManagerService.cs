namespace OneWare.Shared.Services;

public interface IProjectManagerService
{
    public void RegisterProjectManager(Type type, IProjectManager manager);

    public IProjectManager GetManager(Type type);
}