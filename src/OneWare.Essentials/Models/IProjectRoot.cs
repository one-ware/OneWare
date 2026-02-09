namespace OneWare.Essentials.Models;

public interface IProjectRoot : IProjectFolder
{
    public string ProjectTypeId { get; }
    public string ProjectPath { get; }
    public string RootFolderPath { get; }
    public bool IsActive { get; set; }
    public Task InitializeAsync();
    public bool IsPathIncluded(string path);
    public void IncludePath(string path);
    public void OnExternalEntryAdded(string relativePath, FileAttributes attributes);
    public event EventHandler<ProjectEntryOverlayChangedEventArgs>? EntryOverlaysChanged;
    public IReadOnlyList<IconLayer> GetEntryOverlays(IProjectEntry entry);
    public void NotifyEntryOverlayChanged(IProjectEntry entry);
    public void RegisterOverlayProvider(IProjectEntryOverlayProvider provider);
    public void UnregisterOverlayProvider(IProjectEntryOverlayProvider provider);
}
