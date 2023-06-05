using AvaloniaEdit.Document;
using Dock.Model.Core;
using OneWare.Shared.EditorExtensions;

namespace OneWare.Shared;

public interface IEditor : IExtendedDocument
{
    public bool IsReadOnly { get; set; }
    public ExtendedTextEditor Editor { get; }
    public TextDocument CurrentDocument { get; }
    public string FullPath { get; }
    public void Select(int offset, int length);
    public event EventHandler? FileSaved;
}