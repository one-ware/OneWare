using System.Runtime.InteropServices;
using OneWare.Essentials.Models;
using Prism.Ioc;

namespace OneWare.ProjectExplorer.Services;

public class FileWatchService : IFileWatchService
{
    private readonly Dictionary<IFile, FileWatchInstance> _fileWatchInstances = new();
    private readonly Dictionary<IProjectRoot, ProjectWatchInstance> _projectFileWatcher = new();

    public void Register(IFile file)
    {
        if(RuntimeInformation.ProcessArchitecture is Architecture.Wasm) return;
        
        if (_fileWatchInstances.ContainsKey(file)) return;
        _fileWatchInstances.Add(file, ContainerLocator.Container.Resolve<FileWatchInstance>((file.GetType(), file)));
    }

    public void Unregister(IFile file)
    {
        _fileWatchInstances.TryGetValue(file, out var watcher);
        _fileWatchInstances.Remove(file);
        watcher?.Dispose();
    }

    public void Register(IProjectRoot project)
    {
        if(RuntimeInformation.ProcessArchitecture is Architecture.Wasm) return;
        
        if (_projectFileWatcher.ContainsKey(project)) return;
        _projectFileWatcher.Add(project, ContainerLocator.Container.Resolve<ProjectWatchInstance>((project.GetType(), project)));
    }

    public void Unregister(IProjectRoot project)
    {
        _projectFileWatcher.TryGetValue(project, out var watcher);
        _projectFileWatcher.Remove(project);
        watcher?.Dispose();
    }
}