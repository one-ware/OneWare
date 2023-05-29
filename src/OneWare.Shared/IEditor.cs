using AvaloniaEdit;
using AvaloniaEdit.Document;
using Dock.Model.Core;
using OneWare.Shared.EditorExtensions;

namespace OneWare.Shared;

public interface IEditor : IDockable
{
    public ExtendedTextEditor Editor { get; }
    public TextDocument CurrentDocument { get; }
    public IFile CurrentFile { get; init; }
    public bool IsDirty { get; }
    public void Select(int offset, int length);
    public event EventHandler? FileSaved;
}