using OneWare.SDK.Helpers;
using OneWare.SDK.NativeTools;

namespace OneWare.SDK.Services;

public interface INativeToolService
{
    public NativeTool Register(string id, string url, params PlatformId[] supportedPlatforms);

    public NativeTool? Get(string id);
    public Task InstallAsync(NativeTool tool);
}