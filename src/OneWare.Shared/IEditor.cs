using AvaloniaEdit.Document;
using OneWare.Shared.EditorExtensions;

namespace OneWare.Shared;

public interface IEditor : IExtendedDocument
{
    public string FullPath { get; set; }
    public bool IsLoading { get; }
    public bool LoadingFailed { get; }
    public bool IsReadOnly { get; set; }
    public ExtendedTextEditor Editor { get; }
    public TextDocument CurrentDocument { get; }
    public void Select(int offset, int length);
    public event EventHandler? FileSaved;
}