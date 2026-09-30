using System.Text;
using System.Text.RegularExpressions;

namespace OneWare.SearchList.Helpers;

/// <summary>
/// Search and replace logic shared by the find and replace actions of the search list.
/// </summary>
public static class SearchReplaceHelper
{
    public static Regex? TryBuildRegex(string search, bool caseSensitive, bool useRegex, bool wholeWord)
    {
        if (string.IsNullOrEmpty(search)) return null;

        var pattern = useRegex ? search : Regex.Escape(search);
        if (wholeWord) pattern = $@"\b(?:{pattern})\b";

        var options = RegexOptions.Multiline;
        if (!caseSensitive) options |= RegexOptions.IgnoreCase;

        try
        {
            return new Regex(pattern, options);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Converts a replacement string to .NET substitution syntax. .NET syntax (<c>$1</c>, <c>${name}</c>, <c>$0</c>,
    /// <c>$$</c>) is kept as is, and the <c>\1</c> to <c>\9</c> group references used by other editors are converted.
    /// <c>\n</c>, <c>\t</c> and <c>\\</c> insert a newline, a tab and a backslash.
    /// </summary>
    public static string ToRegexReplacement(string replacement)
    {
        if (!replacement.Contains('\\')) return replacement;

        var builder = new StringBuilder(replacement.Length);
        for (var i = 0; i < replacement.Length; i++)
        {
            var c = replacement[i];
            if (c != '\\' || i + 1 >= replacement.Length)
            {
                builder.Append(c);
                continue;
            }

            var next = replacement[i + 1];
            switch (next)
            {
                case >= '0' and <= '9':
                    builder.Append("${").Append(next).Append('}');
                    break;
                case 'n':
                    builder.Append('\n');
                    break;
                case 't':
                    builder.Append('\t');
                    break;
                case '\\':
                    builder.Append('\\');
                    break;
                default:
                    builder.Append(c);
                    continue;
            }

            i++;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Replaces every match. In regex mode the replacement can reference capture groups, otherwise it is inserted literally.
    /// </summary>
    public static string ReplaceAll(string text, Regex regex, string replacement, bool useRegex)
    {
        return useRegex
            ? regex.Replace(text, ToRegexReplacement(replacement))
            : regex.Replace(text, _ => replacement);
    }

    /// <summary>
    /// Replaces the single match at <paramref name="startOffset" />. Returns null if the text there no longer matches,
    /// for example because the file changed since the search.
    /// </summary>
    public static string? ReplaceAt(string text, Regex regex, int startOffset, int length, string replacement,
        bool useRegex)
    {
        if (startOffset < 0 || length <= 0 || startOffset + length > text.Length) return null;

        var match = regex.Match(text, startOffset);
        if (!match.Success || match.Index != startOffset || match.Length != length) return null;

        var value = useRegex ? match.Result(ToRegexReplacement(replacement)) : replacement;
        return string.Concat(text.AsSpan(0, startOffset), value, text.AsSpan(startOffset + length));
    }

    /// <summary>
    /// Finds all non-empty matches with their line for display. Matches that span several lines show their first line.
    /// </summary>
    public static IEnumerable<SearchMatch> FindMatches(string text, Regex regex,
        CancellationToken cancellationToken = default)
    {
        var lineNumber = 1;
        var lineStart = 0;

        foreach (Match match in regex.Matches(text))
        {
            if (cancellationToken.IsCancellationRequested) yield break;
            if (match.Length == 0) continue;

            var index = match.Index;
            for (var newLine = text.IndexOf('\n', lineStart); newLine >= 0 && newLine < index;
                 newLine = text.IndexOf('\n', lineStart))
            {
                lineNumber++;
                lineStart = newLine + 1;
            }

            var lineEnd = text.IndexOf('\n', index);
            if (lineEnd < 0) lineEnd = text.Length;
            var line = text[lineStart..lineEnd].TrimEnd('\r');

            var start = Math.Min(index - lineStart, line.Length);
            var end = Math.Min(index + match.Length - lineStart, line.Length);

            yield return new SearchMatch(index, match.Length, lineNumber, line,
                line[..start].TrimStart(), line[start..end], line[end..].TrimEnd());
        }
    }
}

public readonly record struct SearchMatch(
    int StartOffset,
    int Length,
    int LineNumber,
    string Line,
    string Left,
    string Match,
    string Right);
