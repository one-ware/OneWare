using System.Text.Json.Nodes;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.ProjectSystem;

namespace OneWare.ProjectSystem.Models;

public abstract class UniversalProjectRoot : ProjectRoot, IProjectRootWithFile
{
    private readonly List<Action<IProjectEntry>> _entryModificationHandlers = [];
    
    protected UniversalProjectRoot(string projectFilePath) : base(Path.GetDirectoryName(projectFilePath) ?? throw new NullReferenceException("Invalid Project Path"))
    {
        ProjectFilePath = projectFilePath;

        Icon = new IconModel("UniversalProject");
    }
    
    public UniversalProjectProperties Properties { get; } = new();

    public override string ProjectPath => ProjectFilePath;

    public string ProjectFilePath { get; }

    public DateTime LastSaveTime { get; set; }

    public override bool IsPathIncluded(string path)
    {
        return Properties.IsIncludedPathHelper(path, "include", "exclude");
    }

    public override void IncludePath(string path)
    {
        Properties.AddIncludedPathHelper(path, "include");
    }

    public override IProjectEntry? GetLoadedEntry(string relativePath)
    {
        if (relativePath.Equals(
                Path.GetFileName(ProjectFilePath),
                StringComparison.InvariantCultureIgnoreCase))
            return this;

        return base.GetLoadedEntry(relativePath);
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

    public Task<bool> LoadAsync(IEnumerable<ProjectPropertyMigration>? migrations = null)
    {
        return Properties.LoadAsync(ProjectFilePath, migrations);
    }
    
    public async Task<bool> SaveAsync()
    {
        if (await Properties.SaveAsync(ProjectFilePath))
        {
            LastSaveTime = DateTime.Now;
            return true;
        }
        return false;
    }
}
