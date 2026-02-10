using System.Threading;
using Avalonia.Layout;
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
        return LoadContentAsync();
    }

    public abstract bool IsPathIncluded(string path);

    public abstract void IncludePath(string path);
}