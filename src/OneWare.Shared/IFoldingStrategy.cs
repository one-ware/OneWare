using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace OneWare.Shared;

public interface IFoldingStrategy
{
    void UpdateFoldings(FoldingManager manager, TextDocument document);
}