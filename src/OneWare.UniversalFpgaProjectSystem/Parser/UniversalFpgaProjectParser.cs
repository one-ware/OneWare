using System.Text.Json;
using System.Text.Json.Serialization;
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

            var properties = JsonSerializer.Deserialize<FpgaProjectProperties>(stream, SerializerOptions);
            
            return new UniversalFpgaProjectRoot(path, properties!);
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
            
            return true;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return false;
        }
    }
}