using System.Text.RegularExpressions;

namespace OneWare.Cpp.Folding;

public class FoldingRegexCpp
{
    private const string FoldingStartPattern = @"(?x)
		 /\*\*(?!\*)
		|^(?![^{]*?//|[^{]*?/\*(?!.*?\*/.*?\{)).*?\{\s*($|//|/\*(?!.*?\*/.*\S))";

    private const string FoldingEndPattern = @"(?<!\*)\*\*/|^\s*\}";

    public static readonly Regex FoldingStart = new(FoldingStartPattern, RegexOptions.Multiline);

    public static readonly Regex FoldingEnd = new(FoldingEndPattern, RegexOptions.Multiline);
}