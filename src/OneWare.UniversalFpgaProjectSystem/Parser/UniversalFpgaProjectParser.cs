using System.IO.Enumeration;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using OneWare.Shared;
using OneWare.Shared.Extensions;
using OneWare.Shared.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.Parser;

public static class UniversalFpgaProjectParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowTrailingCommas = true
    };
    
    public static UniversalFpgaProjectRoot? Deserialize(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);

            var properties = JsonSerializer.Deserialize<JsonDocument>(stream, SerializerOptions);

            var includes = properties?.RootElement.GetProperty("Include").Deserialize<string[]>();
            var excludes = properties?.RootElement.GetProperty("Exclude").Deserialize<string[]>();

            var root = new UniversalFpgaProjectRoot(path, properties!);
            ImportFolderRecursive(root.FullPath, root, includes ?? Array.Empty<string>(), excludes ?? Array.Empty<string>());
            return root;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return null;
        }
    }

    private static IEnumerable<(string path, FileAttributes attributes)> GetFileMatches(string source, string[] includeWildcards, string[] excludeWildcards)
    {
        var entries = Directory.EnumerateFileSystemEntries(source);

        foreach (var path in entries)
        {
            var attr = File.GetAttributes(path);
            if (attr.HasFlag(FileAttributes.Hidden)) continue;

            if (attr.HasFlag(FileAttributes.Directory))
            {
                var subDirMatches = GetFileMatches(path, includeWildcards, excludeWildcards);
                foreach (var subMatch in subDirMatches)
                {
                    yield return (Path.Combine(Path.GetFileName(path), subMatch.path), subMatch.attributes);
                }
                continue;
            }
            
            var match = MatchWildCards(path, includeWildcards, excludeWildcards);
            if (match) yield return (Path.GetFileName(path), attr);
        }
    }

    private static bool MatchWildCards(string path, IEnumerable<string> include, IEnumerable<string> exclude)
    {
        return include.Any(includePattern => FileSystemName.MatchesSimpleExpression(includePattern, path))  
               && !exclude.Any(excludePattern => FileSystemName.MatchesSimpleExpression(excludePattern, path));
    }

    private static void ImportFolderRecursive(string source, IProjectFolder destination, string[] includeWildcards, string[] excludeWildcards)
    {
        var matches = GetFileMatches(source, includeWildcards, excludeWildcards);

        foreach (var match in matches)
        {
            if (match.attributes.HasFlag(FileAttributes.Directory))
            {
                destination.AddFolder(match.path);
            }
            else destination.AddFile(match.path);
        }
    }

    public static bool Serialize(UniversalFpgaProjectRoot root)
    {
        try
        {
            using var stream = File.OpenWrite(root.ProjectFilePath);
            stream.SetLength(0);

            JsonSerializer.Serialize(stream, root.Properties, SerializerOptions);
            
            root.LastSaveTime = DateTime.Now;
            
            return true;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return false;
        }
    }
}