using System.Threading;
using Avalonia.Layout;
using Avalonia.Media;
using OneWare.Essentials.Models;

namespace OneWare.ProjectSystem.Models;

public abstract class ProjectRoot : ProjectFolder, IProjectRoot
{
    private readonly List<Action<IProjectEntry>> _entryModificationHandlers = [];

    protected ProjectRoot(string rootFolderPath) : base(Path.GetFileName(rootFolderPath),
        null)
    {
        RootFolderPath = rootFolderPath;
        TopFolder = this;

        // The "loading failed" overlay is applied through the entry modification pipeline so it
        // survives the virtualized project explorer recycling containers and swapping icon models.
        RegisterProjectEntryModification(x =>
        {
            if (x.LoadingFailed)
            {
                x.Icon?.AddOverlay("LoadingFailed", "VsImageLib.StatusCriticalErrorOverlayExp16X");
            }
            else
            {
                x.Icon?.RemoveOverlay("LoadingFailed");
            }
        });
    }

    public void RegisterProjectEntryModification(Action<IProjectEntry> modificationAction)
    {
        _entryModificationHandlers.Add(modificationAction);
    }

    public void InvalidateModifications(IProjectEntry entry)
    {
        _entryModificationHandlers.ForEach(handler => handler(entry));
    }

    /// <summary>
    /// Re-runs all registered entry modifications for every loaded entry in the project.
    /// Useful when a project wide state (e.g. the top entity) changes and the affected
    /// entries are not known upfront.
    /// </summary>
    public void InvalidateAllModifications()
    {
        foreach (var entry in EnumerateLoadedEntries(this))
            InvalidateModifications(entry);
    }

    private static IEnumerable<IProjectEntry> EnumerateLoadedEntries(IProjectExplorerNode node)
    {
        if (node.Children == null) yield break;

        foreach (var child in node.Children)
        {
            if (child is IProjectEntry entry) yield return entry;
            foreach (var descendant in EnumerateLoadedEntries(child)) yield return descendant;
        }
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