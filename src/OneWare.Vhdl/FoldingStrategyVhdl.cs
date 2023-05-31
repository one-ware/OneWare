using OneWare.Shared.EditorExtensions;

namespace OneWare.Vhdl;

public class FoldingStrategyVhdl : FoldingStrategyBase
{
    public FoldingStrategyVhdl()
    {
        Foldings.Add(new FoldingEntry("(", ")"));
        Foldings.Add(new FoldingEntry("/*", "*/"));
    }
}