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
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
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

    private static void ImportFolderRecursive(string source, IProjectFolder destination, string[] include, string[] exclude)
    {
        var entries = Directory.GetFileSystemEntries(source);
        foreach (var entry in entries)
        {
            var attr = File.GetAttributes(entry);
            
            if (attr.HasFlag(FileAttributes.Hidden)) continue;

            try
            {
                var incl = include.Any(includePattern => FileSystemName.MatchesSimpleExpression(includePattern, entry));
                if (!incl || exclude.Any(excludePattern => FileSystemName.MatchesSimpleExpression(excludePattern, entry))) return;
            }
            catch
            {
                return;
            }

            if (attr.HasFlag(FileAttributes.Directory))
            {
                if(entry.EqualPaths(destination.FullPath)) continue;
                var folder = destination.AddFolder(Path.GetFileName(entry));
                ImportFolderRecursive(entry, folder, include, exclude);
            }
            else
            {
                destination.ImportFile(entry);
            }
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