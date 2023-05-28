using AvaloniaEdit;
using AvaloniaEdit.Document;
using Dock.Model.Core;

namespace OneWare.Shared;

public interface IEditor : IDockable
{
    public TextDocument CurrentDocument { get; }
    public IFile CurrentFile { get; init; }
    public bool IsDirty { get; }
    public void Select(int offset, int length);
}