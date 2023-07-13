using OneWare.Shared.EditorExtensions;

namespace OneWare.Json.Folding;

public class FoldingStrategyJson : FoldingStrategyBase
{
    public FoldingStrategyJson()
    {
        Foldings.Add(new FoldingEntry("{", "}"));
    }
}