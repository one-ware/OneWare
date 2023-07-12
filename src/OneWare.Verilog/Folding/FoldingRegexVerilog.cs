using System.Text.RegularExpressions;

namespace OneWare.Verilog.Folding;

public class FoldingRegexVerilog
{
    private const string FoldingStartPattern = @"(begin)\s*(//.*)?$";

    private const string FoldingEndPattern = @"^\s*(end)$";

    public static readonly Regex FoldingStart = new(FoldingStartPattern);

    public static readonly Regex FoldingEnd = new(FoldingEndPattern);
}