using System.IO.Enumeration;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Helpers;

public static class ProjectHelper
{
    public static void ImportEntries(string source, IProjectFolder destination)
    {
        var matches = GetFileMatches(source, x => destination.Root.IsPathIncluded(Path.GetRelativePath(destination.Root.FullPath, x)));

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
        var entries = Directory.EnumerateFileSystemEntries(source, "**", SearchOption.AllDirectories);
        
        foreach (var path in entries)
        {
            var attr = File.GetAttributes(path);
            
            var match = valid?.Invoke(path) ?? true;
            if (match) yield return (path, attr);
        }
    }
        
    public static bool MatchWildCards(string path, IEnumerable<string> include, IEnumerable<string>? exclude)
    {
        return include.Any(includePattern => FileSystemName.MatchesSimpleExpression(includePattern, path))  
               && (exclude is null || !exclude.Any(excludePattern => FileSystemName.MatchesSimpleExpression(excludePattern, path) 
                        || Enumerable
                            .SkipLast<string>(path.ToUnixPath()
                                .Split('/', StringSplitOptions.RemoveEmptyEntries), 1)
                            .Any(x => FileSystemName.MatchesSimpleExpression(excludePattern, x))));
    }
}