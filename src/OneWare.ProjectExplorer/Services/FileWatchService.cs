using System.Runtime.InteropServices;
using OneWare.Essentials.Models;

namespace OneWare.ProjectExplorer.Services;

public class FileWatchService : IFileWatchService
{
    private readonly Dictionary<IFile, FileWatchInstance> _fileWatchInstances = new();
    private readonly Dictionary<IProjectRoot, ProjectWatchInstance> _projectFileWatcher = new();

    private readonly Func<IFile, FileWatchInstance> _fileWatchInstanceFactory;
    private readonly Func<IProjectRoot, ProjectWatchInstance> _projectWatchInstanceFactory;

    public FileWatchService(
        Func<IFile, FileWatchInstance> fileWatchInstanceFactory,
        Func<IProjectRoot, ProjectWatchInstance> projectWatchInstanceFactory)
    {
        _fileWatchInstanceFactory = fileWatchInstanceFactory;
        _projectWatchInstanceFactory = projectWatchInstanceFactory;
    }

    public void Register(IFile file)
    {
        if (RuntimeInformation.ProcessArchitecture is Architecture.Wasm) return;

        if (_fileWatchInstances.ContainsKey(file)) return;

        var instance = _fileWatchInstanceFactory(file);
        _fileWatchInstances.Add(file, instance);
    }

    public void Unregister(IFile file)
    {
        if (_fileWatchInstances.TryGetValue(file, out var watcher))
        {
            _fileWatchInstances.Remove(file);
            watcher.Dispose();
        }
    }

    public void Register(IProjectRoot project)
    {
        if (RuntimeInformation.ProcessArchitecture is Architecture.Wasm) return;

        if (_projectFileWatcher.ContainsKey(project)) return;

        var instance = _projectWatchInstanceFactory(project);
        _projectFileWatcher.Add(project, instance);
    }

    public void Unregister(IProjectRoot project)
    {
        if (_projectFileWatcher.TryGetValue(project, out var watcher))
        {
            _projectFileWatcher.Remove(project);
            watcher.Dispose();
        }
    }
}
