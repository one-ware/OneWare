using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace OneWare.Shared.EditorExtensions;

public interface IFoldingStrategy
{
    void UpdateFoldings(FoldingManager manager, TextDocument document);
}