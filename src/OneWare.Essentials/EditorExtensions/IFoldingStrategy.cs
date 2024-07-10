using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace OneWare.Essentials.EditorExtensions;

public interface IFoldingStrategy
{
    void UpdateFoldings(FoldingManager manager, TextDocument document);
}