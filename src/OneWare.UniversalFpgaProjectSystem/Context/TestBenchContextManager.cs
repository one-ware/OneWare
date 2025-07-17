using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.Context;

public static class TestBenchContextManager
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    private static string GetSaveFilePath(string tbPath)
    {
        return Path.Combine(Path.GetDirectoryName(tbPath) ?? "", Path.GetFileNameWithoutExtension(tbPath) + ".tbconf");
    }

    public static async Task<TestBenchContext> LoadContextAsync(IFile file)
    {
        var path = GetSaveFilePath(file.FullPath);

        if (File.Exists(path))
            try
            {
                await using var stream = File.OpenRead(path);

                var properties = await JsonNode.ParseAsync(stream);

                return new TestBenchContext(file, properties as JsonObject ?? new JsonObject());
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>().LogError(e, e.Message);
            }

        return new TestBenchContext(file, new JsonObject());
    }

    public static async Task<bool> SaveContextAsync(TestBenchContext context)
    {
        try
        {
            var path = GetSaveFilePath(context.File.FullPath);

            if (!File.Exists(path) && context.Properties.Count == 0) return false;

            await using var stream = File.OpenWrite(path);
            stream.SetLength(0);
            await JsonSerializer.SerializeAsync(stream, context.Properties, Options);
            return true;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().LogError(e, e.Message);
            return false;
        }
    }
}