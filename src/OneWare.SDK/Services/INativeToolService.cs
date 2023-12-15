using OneWare.SDK.Helpers;
using OneWare.SDK.NativeTools;

namespace OneWare.SDK.Services;

public interface INativeToolService
{
    public NativeToolContainer Register(string key);
    public NativeToolContainer? Get(string key);
    public Task<bool> InstallAsync(NativeToolContainer container);
}