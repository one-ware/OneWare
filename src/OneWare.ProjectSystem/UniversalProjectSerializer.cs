using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
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
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static async Task<UniversalProjectProperties?> DeserializePropertiesAsync(string path)
    {
        try
        {
            await using var stream = File.OpenRead(path);

            var properties =
                await JsonSerializer.DeserializeAsync<UniversalProjectProperties>(stream, SerializerOptions);
            
            return properties;
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
            await JsonSerializer.SerializeAsync(stream, root.Properties, SerializerOptions);
            
            return true;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return false;
        }
    }
}
