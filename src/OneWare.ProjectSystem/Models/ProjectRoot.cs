using Avalonia.Media;
using Avalonia.Threading;
using OneWare.Shared;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.ProjectSystem.Models;

public abstract class ProjectRoot : ProjectFolder, IProjectRoot
{
    public abstract string ProjectPath { get; }
    public abstract string ProjectTypeId { get; }
    public string RootFolderPath { get; }
    public List<IProjectFile> Files { get; } = new();
    public override string FullPath => RootFolderPath;

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set
        {
            SetProperty(ref _isActive, value);
            FontWeight = value ? FontWeight.Bold : FontWeight.Regular;
        }
    }

    protected ProjectRoot(string rootFolderPath) : base(Path.GetFileName(rootFolderPath), null)
    {
        RootFolderPath = rootFolderPath;
        TopFolder = this;
    }

    internal void RegisterEntry(IProjectEntry entry)
    {
        if(entry is ProjectFile file) Files.Add(file);
    }
    
    internal void UnregisterEntry(IProjectEntry entry)
    {
        if (entry is ProjectFile file) Files.Remove(file);
    }

    public abstract bool IsPathIncluded(string path);
}