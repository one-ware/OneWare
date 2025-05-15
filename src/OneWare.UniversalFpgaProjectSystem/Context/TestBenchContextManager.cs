using System.Text.Json;
using System.Text.Json.Nodes;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.UniversalFpgaProjectSystem.Context;

public class TestBenchContextManager
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    private readonly ILogger _logger;

    public TestBenchContextManager(ILogger logger)
    {
        _logger = logger;
    }

    private static string GetSaveFilePath(string tbPath)
    {
        return Path.Combine(Path.GetDirectoryName(tbPath) ?? "", Path.GetFileNameWithoutExtension(tbPath) + ".tbconf");
    }

    public async Task<TestBenchContext> LoadContextAsync(IFile file)
    {
        var path = GetSaveFilePath(file.FullPath);

        if (File.Exists(path))
        {
            try
            {
                await using var stream = File.OpenRead(path);

                var properties = await JsonNode.ParseAsync(stream);

                return new TestBenchContext(file, properties as JsonObject ?? new JsonObject());
            }
            catch (Exception e)
            {
                _logger.Error(e.Message, e);
            }
        }

        return new TestBenchContext(file, new JsonObject());
    }

    public async Task<bool> SaveContextAsync(TestBenchContext context)
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
            _logger.Error(e.Message, e);
            return false;
        }
    }
}
