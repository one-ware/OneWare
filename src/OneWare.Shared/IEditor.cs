using AvaloniaEdit.Document;
using OneWare.Shared.EditorExtensions;

namespace OneWare.Shared;

public interface IEditor : IExtendedDocument
{
    public ExtendedTextEditor Editor { get; }
    public TextDocument CurrentDocument { get; }
    public void Select(int offset, int length);
    public event EventHandler? FileSaved;
}