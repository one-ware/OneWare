namespace OneWare.Essentials.Models;

public sealed class ProjectEntryOverlayChangedEventArgs(IProjectEntry entry) : EventArgs
{
    public IProjectEntry Entry { get; } = entry;
}
