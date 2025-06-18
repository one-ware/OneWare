using System.Text.Json;
using OneWare.Essentials.Services;

namespace OneWare.Vcd.Viewer.Context
{
    public class VcdContextManager
    {
        private readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true
        };

        private readonly ILogger _logger;

        public VcdContextManager(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<VcdContext?> LoadContextAsync(string path)
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
                    _logger.Error(e.Message, e);
                }
            }

            return null;
        }

        public async Task<bool> SaveContextAsync(string path, VcdContext context)
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
                _logger.Error(e.Message, e);
                return false;
            }
        }
    }
}
