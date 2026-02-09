namespace OneWare.Essentials.Models;

public class ProjectPropertyChangedEventArgs(
    string propertyName,
    object? oldValue,
    object? newValue) : EventArgs
{
    public string PropertyName { get; } = propertyName;
    public object? OldValue { get; } = oldValue;
    public object? NewValue { get; } = newValue;
}

public interface IProjectRootWithFile : IProjectRoot
{
    public event EventHandler<ProjectPropertyChangedEventArgs>? ProjectPropertyChanged;
    public DateTime LastSaveTime { get; }
    public string ProjectFilePath { get; }
    public void RegisterEntryModification(Action<IProjectEntry> modificationAction);
    public void InvalidateModifications(IProjectEntry entry);
}