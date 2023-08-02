using System.IO.Enumeration;

namespace OneWare.Shared.Helpers;

public static class ProjectHelpers
{
    public static void ImportEntries(string source, IProjectFolder destination)
    {
        var matches = GetFileMatches(source, destination.Root.IsPathIncluded);

        foreach (var match in matches)
        {
            var relativePath = Path.GetRelativePath(destination.FullPath, match.path);
            if (match.attributes.HasFlag(FileAttributes.Directory))
            {
                destination.AddFolder(relativePath);
            }
            else destination.AddFile(relativePath);
        }
    }

    private static IEnumerable<(string path, FileAttributes attributes)> GetFileMatches(string source, Func<string,bool>? valid = null)
    {
        var entries = Directory.EnumerateFileSystemEntries(source);

        foreach (var path in entries)
        {
            var attr = File.GetAttributes(path);
            if (attr.HasFlag(FileAttributes.Hidden)) continue;

            if (attr.HasFlag(FileAttributes.Directory))
            {
                var subDirMatches = GetFileMatches(path,valid);
                foreach (var subMatch in subDirMatches)
                {
                    yield return subMatch;
                }
            }
            
            var match = valid?.Invoke(path) ?? true;
            if (match) yield return (path, attr);
        }
    }
        
    public static bool MatchWildCards(string path, IEnumerable<string> include, IEnumerable<string>? exclude)
    {
        return include.Any(includePattern => FileSystemName.MatchesSimpleExpression(includePattern, path))  
               && (exclude is null || !exclude.Any(excludePattern => FileSystemName.MatchesSimpleExpression(excludePattern, path)));
    }
}