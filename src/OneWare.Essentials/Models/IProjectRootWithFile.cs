namespace OneWare.Essentials.Models;

public interface IProjectRootWithFile : IProjectRoot
{
    public UniversalProjectProperties Properties { get; }
    public DateTime LastSaveTime { get; }
    public string ProjectFilePath { get; }
    public void RegisterEntryModification(Action<IProjectEntry> modificationAction);
    public void InvalidateModifications(IProjectEntry entry);
}