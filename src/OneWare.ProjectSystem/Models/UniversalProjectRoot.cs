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

    public Task<bool> LoadAsync()
    {
        return Properties.LoadAsync(ProjectFilePath);
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
