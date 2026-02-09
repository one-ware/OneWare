using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using OneWare.ProjectSystem.Models;

namespace OneWare.ProjectSystem;

public static class UniversalProjectSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static async Task<UniversalProjectProperties?> DeserializePropertiesAsync(string path)
    {
        try
        {
            await using var stream = File.OpenRead(path);

            var node = await JsonNode.ParseAsync(stream, null, new JsonDocumentOptions
            {
                AllowTrailingCommas = true
            });

            if (node is not JsonObject obj)
                return new UniversalProjectProperties();

            return new UniversalProjectProperties(obj);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return null;
        }
    }

    public static async Task<bool> SerializeAsync(UniversalProjectRoot root)
    {
        try
        {
            await using var stream = File.OpenWrite(root.ProjectFilePath);
            stream.SetLength(0);
            await JsonSerializer.SerializeAsync(stream, root.Properties.AsObject(), SerializerOptions);
            
            return true;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return false;
        }
    }
}
