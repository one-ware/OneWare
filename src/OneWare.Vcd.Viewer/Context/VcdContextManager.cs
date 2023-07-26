using System.Linq.Expressions;
using System.Text.Json;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.Vcd.Viewer.Context;

public static class VcdContextManager
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions()
    {
        WriteIndented = true
    };
    
    public static async Task<VcdContext?> LoadContextAsync(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                await using var stream = File.OpenRead(path);
                return await JsonSerializer.DeserializeAsync<VcdContext>(stream, Options);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            }
        }
        return null;
    }

    public static async Task<bool> SaveContextAsync(string path, VcdContext context)
    {
        try
        {
            await using var stream = File.OpenWrite(path);
            stream.SetLength(0);
            await JsonSerializer.SerializeAsync(stream, context, Options);
            return true;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return false;
        }
    }
}