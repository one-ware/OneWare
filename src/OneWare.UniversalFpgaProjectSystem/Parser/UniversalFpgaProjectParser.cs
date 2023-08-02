using System.IO.Enumeration;
using System.Text.Json;
using System.Text.Json.Nodes;
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

            var properties = JsonSerializer.Deserialize<JsonObject>(stream, SerializerOptions);

            var root = new UniversalFpgaProjectRoot(path, properties!);
            return root;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return null;
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