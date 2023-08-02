using System.Text.RegularExpressions;

namespace OneWare.Json.Folding;

public class FoldingRegexJson
{
    private const string FoldingStartPattern = @"(?x)       # turn on extended mode
  ^        # a line beginning with
  \s*      # some optional space
  [{\[]    # the start of an object or array
  (?!      # but not followed by
    .*     # whatever
    [}\]]  # and the close of an object or array
    ,?     # an optional comma
    \s*    # some optional space
    $      # at the end of the line
  )
  |        # ...or...
  [{\[]    # the start of an object or array
  \s*      # some optional space
  $        # at the end of the line
	";

    private const string FoldingEndPattern = @"(?x)     # turn on extended mode
  ^      # a line beginning with
  \s*    # some optional space
  [}\]]  # and the close of an object or array
	";

    public static readonly Regex FoldingStart = new(FoldingStartPattern, RegexOptions.Multiline);

    public static readonly Regex FoldingEnd = new(FoldingEndPattern, RegexOptions.Multiline);
}