using System.Threading;
using Avalonia.Media;
using OneWare.Essentials.Models;

namespace OneWare.ProjectSystem.Models;

public abstract class ProjectRoot : ProjectFolder, IProjectRoot
{
    protected ProjectRoot(string rootFolderPath) : base(Path.GetFileName(rootFolderPath),
        null)
    {
        RootFolderPath = rootFolderPath;
        TopFolder = this;
    }

    public abstract string ProjectPath { get; }

    public abstract string ProjectTypeId { get; }
    public string RootFolderPath { get; }
    public override string FullPath => RootFolderPath;

    public bool IsActive
    {
        get;
        set
        {
            SetProperty(ref field, value);
            FontWeight = value ? FontWeight.Bold : FontWeight.Regular;
        }
    }

    public Task InitializeAsync()
    {
        return LoadContentAsync(CancellationToken.None, true);
    }

    public abstract bool IsPathIncluded(string path);
    
    public abstract void IncludePath(string path);

    public virtual void OnExternalEntryAdded(string relativePath, FileAttributes attributes)
    {
        if (!IsPathIncluded(relativePath)) return;
        
        var parentPath = Path.GetDirectoryName(relativePath);
        if (parentPath != null && GetLoadedEntry(parentPath) is IProjectFolder folder)
        {
            if (folder.IsExpanded)
            {
                if (attributes.HasFlag(FileAttributes.Directory))
                    AddFolder(relativePath);
                else
                    AddFile(relativePath);
            }
            else if (folder.Children.Count == 0)
            {
                folder.Children.Add(new LoadingDummyNode());
            }
        }
    }
}
