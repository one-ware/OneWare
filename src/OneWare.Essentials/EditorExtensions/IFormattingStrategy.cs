using AvaloniaEdit.Document;

namespace OneWare.Essentials.EditorExtensions;

public interface IFormattingStrategy
{
    public void Format(TextDocument document);
}