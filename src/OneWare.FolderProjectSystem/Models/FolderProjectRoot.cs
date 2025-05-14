using DynamicData.Binding;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.ProjectSystem.Models;
using Autofac;

namespace OneWare.FolderProjectSystem.Models;

public class FolderProjectRoot : ProjectRoot
{
    public const string ProjectType = "Folder";

    private readonly Dictionary<IProjectFolder, IDisposable> _registeredFolders = new();
    private readonly ILogger _logger; // ILogger injected via constructor

    // Constructor now accepts ILogger via dependency injection
    public FolderProjectRoot(string rootFolderPath, ILogger logger) : base(rootFolderPath, true)
    {
        _logger = logger;
        WatchDirectory(this);
    }

    public override string ProjectPath => RootFolderPath;
    public override string ProjectTypeId => ProjectType;

    public override void RegisterEntry(IProjectEntry entry)
    {
        if (entry is IProjectFolder folder) WatchDirectory(folder);
        base.RegisterEntry(entry);
    }

    public override void UnregisterEntry(IProjectEntry entry)
    {
        if (entry is IProjectFolder folder) UnwatchDirectory(folder);
        base.UnregisterEntry(entry);
    }

    private void WatchDirectory(IProjectFolder folder)
    {
        try
        {
            var subscription = folder.WhenValueChanged(x => x.IsExpanded).Subscribe(x =>
            {
                if (x)
                {
                    if (folder.Children.FirstOrDefault() is LoadingDummyNode) folder.Children.RemoveAt(0);
                    FolderProjectManager.LoadFolder(folder);
                }
                else
                {
                    if (folder.Entities.Count > 0)
                        foreach (var subEntity in folder.Entities.ToArray())
                            folder.Remove(subEntity);
                    if (Directory.Exists(folder.FullPath) &&
                        Directory.EnumerateFileSystemEntries(folder.FullPath).Any())
                        folder.Children.Add(new LoadingDummyNode());
                }
            });

            _registeredFolders.Add(folder, subscription);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e); // Using injected logger
        }
    }

    private void UnwatchDirectory(IProjectFolder folder)
    {
        if (_registeredFolders.TryGetValue(folder, out var subscription))
        {
            subscription.Dispose();
            _registeredFolders.Remove(folder);
        }
    }

    public override bool IsPathIncluded(string path)
    {
        return true;
    }

    public override void IncludePath(string path)
    {
        //Not needed
    }

    public override void OnExternalEntryAdded(string path, FileAttributes attributes)
    {
        var parentPath = Path.GetDirectoryName(path);

        if (parentPath != null && SearchFullPath(parentPath) is IProjectFolder parent)
        {
            var relativePath = Path.GetRelativePath(FullPath, path);

            if (attributes.HasFlag(FileAttributes.Directory))
                AddFolder(relativePath);
            else
                AddFile(relativePath);
        }
    }
}
