using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace OneWare.Core.EditorExtensions;

public interface IFoldingStrategy
{
    void UpdateFoldings(FoldingManager manager, TextDocument document);
}