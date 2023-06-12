using AvaloniaEdit.Document;
using Dock.Model.Core;
using OneWare.Shared.EditorExtensions;

namespace OneWare.Shared;

public interface IEditor : IExtendedDocument
{
    public string FullPath { get; set; }
    public bool IsReadOnly { get; set; }
    public ExtendedTextEditor Editor { get; }
    public TextDocument CurrentDocument { get; }
    public void Select(int offset, int length);
    public event EventHandler? FileSaved;
}