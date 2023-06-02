using OneWare.Shared.EditorExtensions;

namespace OneWare.Cpp;

public class FoldingStrategyCpp : FoldingStrategyBase
{
    public FoldingStrategyCpp()
    {
        Foldings.Add(new FoldingEntry("{", "}"));
        Foldings.Add(new FoldingEntry("(", ")"));
        Foldings.Add(new FoldingEntry("/*", "*/"));
        Foldings.Add(new FoldingEntry("#region", "#endregion"));
    }
}