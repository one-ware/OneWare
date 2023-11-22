using AvaloniaEdit.Document;

namespace OneWare.SDK.EditorExtensions;

public interface IFormattingStrategy
{
    public void Format(TextDocument document);
}