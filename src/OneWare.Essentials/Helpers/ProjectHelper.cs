using System.IO.Enumeration;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Helpers;

public static class ProjectHelper
{
    private static IEnumerable<(string path, FileAttributes attributes)> GetFileMatches(string source,
        Func<string, bool>? valid = null)
    {
        var entries = Directory.EnumerateFileSystemEntries(source, "**", SearchOption.AllDirectories);

        foreach (var path in entries)
        {
            var attr = File.GetAttributes(path);

            var match = valid?.Invoke(path) ?? true;
            if (match) yield return (path, attr);
        }
    }

    public static bool MatchWildCards(
        string path,
        IEnumerable<string> include,
        IEnumerable<string>? exclude)
    {
        var normalizedPath = path.ToUnixPath();

        return include.Any(pattern =>
                   FileSystemName.MatchesSimpleExpression(pattern, normalizedPath))
               && (exclude is null || !exclude.Any(pattern =>
                   FileSystemName.MatchesSimpleExpression(pattern, normalizedPath)
                   || normalizedPath
                       .Split('/', StringSplitOptions.RemoveEmptyEntries)
                       .SkipLast(1)
                       .Any(dir =>
                           FileSystemName.MatchesSimpleExpression(pattern, dir))));
    }
}