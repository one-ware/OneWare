using OneWare.Shared;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.ProjectExplorer.Services;

public class FileWatchService : IFileWatchService
{
    private readonly Dictionary<IProjectRoot, FileWatchInstance> _fileWatcher = new();

    public void Register(IProjectRoot project)
    {
        if (_fileWatcher.ContainsKey(project)) return;
        _fileWatcher.Add(project, ContainerLocator.Container.Resolve<FileWatchInstance>((project.GetType(), project)));
    }

    public void Unregister(IProjectRoot project)
    {
        _fileWatcher.TryGetValue(project, out var watcher);
        _fileWatcher.Remove(project);
        watcher?.Dispose();
    }
}