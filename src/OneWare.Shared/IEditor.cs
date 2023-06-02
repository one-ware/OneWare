using AvaloniaEdit.Document;
using Dock.Model.Core;
using OneWare.Shared.EditorExtensions;

namespace OneWare.Shared;

public interface IEditor : IDockable
{
    public bool IsReadOnly { get; set; }
    public ExtendedTextEditor Editor { get; }
    public TextDocument CurrentDocument { get; }
    public IFile CurrentFile { get; }
    public bool IsDirty { get; }
    public void Select(int offset, int length);
    public event EventHandler? FileSaved;
}