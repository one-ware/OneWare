namespace OneWare.Essentials.Models;

public interface IProjectRoot : IProjectFolder
{
    public string ProjectTypeId { get; }
    public string ProjectPath { get; }
    public string RootFolderPath { get; }
    public bool IsActive { get; set; }
    public bool IsPathIncluded(string path);
    public void IncludePath(string path);

    /// <summary>
    /// Registers an action that is (re-)applied to project entries through the modification
    /// pipeline. Handlers are re-run whenever an entry is (re)realized in the virtualized
    /// project explorer, so they must be idempotent.
    /// </summary>
    public void RegisterProjectEntryModification(Action<IProjectEntry> modificationAction);

    /// <summary>
    /// Re-runs all registered entry modifications for the given entry.
    /// </summary>
    public void InvalidateModifications(IProjectEntry entry);
}
