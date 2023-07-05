namespace OneWare.Shared;

public interface IProjectManager
{
    public IProjectRoot? LoadProject(string path);

    public bool SaveProject(IProjectRoot root);
}