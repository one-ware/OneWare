namespace OneWare.Essentials.ViewModels;

/// <summary>
///     Marks a document that detects and applies external changes of its file itself.
///     Project watchers will not reload such documents when the file changes on disk.
/// </summary>
public interface ISelfWatchingDocument
{
}
