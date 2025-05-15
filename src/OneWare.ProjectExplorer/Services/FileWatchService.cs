using System.Runtime.InteropServices;
using OneWare.Essentials.Models;

namespace OneWare.ProjectExplorer.Services;

public class FileWatchService : IFileWatchService
{
    private readonly Dictionary<IFile, FileWatchInstance> _fileWatchInstances = new();
    private readonly Dictionary<IProjectRoot, ProjectWatchInstance> _projectFileWatcher = new();

    private readonly Func<IFile, FileWatchInstance> _fileWatchFactory;
    private readonly Func<IProjectRoot, ProjectWatchInstance> _projectWatchFactory;

    public FileWatchService(
        Func<IFile, FileWatchInstance> fileWatchFactory,
        Func<IProjectRoot, ProjectWatchInstance> projectWatchFactory)
    {
        _fileWatchFactory = fileWatchFactory;
        _projectWatchFactory = projectWatchFactory;
    }

    public void Register(IFile file)
    {
        if (RuntimeInformation.ProcessArchitecture is Architecture.Wasm) return;

        if (_fileWatchInstances.ContainsKey(file)) return;

        _fileWatchInstances.Add(file, _fileWatchFactory(file));
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

        _projectFileWatcher.Add(project, _projectWatchFactory(project));
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
