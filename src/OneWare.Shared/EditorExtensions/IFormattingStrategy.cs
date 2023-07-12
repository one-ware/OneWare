using AvaloniaEdit.Document;

namespace OneWare.Shared.EditorExtensions;

public interface IFormattingStrategy
{
    public void Format(TextDocument document);
}