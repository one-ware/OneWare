using System.Runtime.InteropServices;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.ProjectExplorer.Services;

public class FileWatchService : IFileWatchService
{
    private readonly Dictionary<string, FileWatchInstance> _fileWatchInstances = new();
    private readonly Dictionary<IProjectRoot, ProjectWatchInstance> _projectFileWatcher = new();

    public void Register(IFile file)
    {
        if (RuntimeInformation.ProcessArchitecture is Architecture.Wasm) return;

        var fileKey = file.FullPath.ToPathKey();
        if (_fileWatchInstances.ContainsKey(fileKey)) return;
        _fileWatchInstances.Add(fileKey,
            ContainerLocator.Container.Resolve<FileWatchInstance>((file.GetType(), file)));
    }

    public void Unregister(IFile file)
    {
        var fileKey = file.FullPath.ToPathKey();
        _fileWatchInstances.TryGetValue(fileKey, out var watcher);
        _fileWatchInstances.Remove(fileKey);
        watcher?.Dispose();
    }

    public void Register(IProjectRoot project)
    {
        if (RuntimeInformation.ProcessArchitecture is Architecture.Wasm) return;

        if (_projectFileWatcher.ContainsKey(project)) return;
        _projectFileWatcher.Add(project,
            ContainerLocator.Container.Resolve<ProjectWatchInstance>((project.GetType(), project)));
    }

    public void Unregister(IProjectRoot project)
    {
        _projectFileWatcher.TryGetValue(project, out var watcher);
        _projectFileWatcher.Remove(project);
        watcher?.Dispose();
    }
}
