using System.Text.RegularExpressions;

namespace OneWare.Essentials.Helpers;

/// <summary>
///     Resolves link targets (e.g. from markdown) to files or folders on disk. Accepts absolute and relative paths,
///     <c>file://</c> URIs, <c>~/</c>, percent-encoding and an optional line suffix (<c>:12</c>, <c>:12:5</c> or
///     <c>#L12</c>).
/// </summary>
public static partial class LocalLinkResolver
{
    // A URI scheme with at least two characters (so C:\ stays a path) that isn't just a line suffix (main.c:12)
    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+.\-]+:(?!\d+(:\d+)?$)")]
    private static partial Regex ExternalSchemeRegex();

    [GeneratedRegex(@"^(?<path>.+?)(?::(?<line>\d+)(?::\d+)?|#L(?<line>\d+)(?:C\d+)?(?:-L?\d+(?:C\d+)?)?)$")]
    private static partial Regex LineSuffixRegex();

    /// <summary>
    ///     True for links that should be handed to the OS (http, mailto, ...), false for file paths and file URIs.
    /// </summary>
    public static bool IsExternalLink(string link)
    {
        link = link.Trim();
        return !link.StartsWith("file:", StringComparison.OrdinalIgnoreCase) && ExternalSchemeRegex().IsMatch(link);
    }

    public static bool TryResolve(string link, IEnumerable<string?> baseDirectories, out string fullPath,
        out int? line)
    {
        fullPath = string.Empty;
        line = null;

        link = link.Trim();
        if (link.Length >= 2 && link[0] == '<' && link[^1] == '>') link = link[1..^1];
        if (link.Length == 0 || link.StartsWith('#') || IsExternalLink(link)) return false;

        if (link.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || !uri.IsFile) return false;
            link = uri.LocalPath + uri.Fragment;
        }

        var bases = baseDirectories.Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToArray();

        foreach (var candidate in Candidates(link))
        {
            if (TryFind(candidate, bases, out fullPath)) return true;

            var match = LineSuffixRegex().Match(candidate);
            if (match.Success && TryFind(match.Groups["path"].Value, bases, out fullPath))
            {
                line = int.Parse(match.Groups["line"].Value);
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> Candidates(string link)
    {
        yield return link;

        string unescaped;
        try
        {
            unescaped = Uri.UnescapeDataString(link);
        }
        catch (UriFormatException)
        {
            yield break;
        }

        if (unescaped != link) yield return unescaped;
    }

    private static bool TryFind(string path, string[] baseDirectories, out string fullPath)
    {
        fullPath = string.Empty;

        if (path == "~" || path.StartsWith("~/") || path.StartsWith("~\\"))
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.TrimStart('~')
                .TrimStart('/', '\\'));

        try
        {
            if (Path.IsPathRooted(path)) return Exists(Path.GetFullPath(path), out fullPath);

            foreach (var baseDirectory in baseDirectories)
                if (Exists(Path.GetFullPath(Path.Combine(baseDirectory, path)), out fullPath))
                    return true;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Not a valid path on this platform
        }

        return false;
    }

    private static bool Exists(string path, out string fullPath)
    {
        fullPath = path;
        return File.Exists(path) || Directory.Exists(path);
    }
}
