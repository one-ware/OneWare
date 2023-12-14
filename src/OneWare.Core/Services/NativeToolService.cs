using OneWare.SDK.Helpers;
using OneWare.SDK.NativeTools;
using OneWare.SDK.Services;

namespace OneWare.Core.Services;

public class NativeToolService(IHttpService httpService, ISettingsService settingsService, IPaths paths, ILogger logger) : INativeToolService
{
    private readonly Dictionary<PlatformId, Dictionary<string, NativeTool>> _nativeTools = new();
    
    public NativeTool Register(string id, string url, params PlatformId[] supportedPlatforms)
    {
        var nativeTool = new NativeTool(id, url, Path.Combine(paths.NativeToolsDirectory, id));
        foreach (var platform in supportedPlatforms)
        {
            _nativeTools.TryAdd(platform, new Dictionary<string, NativeTool>());
            _nativeTools[platform].Add(id, nativeTool);
        }
        return nativeTool;
    }

    public NativeTool? Get(string key)
    {
        if (_nativeTools.TryGetValue(PlatformHelper.Platform, out var platformTools))
        {
            if (platformTools.TryGetValue(key, out var nativeTool))
            {
                return nativeTool;
            }
        }
        return null;
    }

    public async Task InstallAsync(NativeTool tool)
    {
        logger.Log($"Downloading {tool.Id}...");
        
        await DownloadNativeToolAsync(tool);

        foreach (var shortCut in tool.ShortCuts)
        {
            if(shortCut.Value.SettingKey != null)
                settingsService.SetSettingValue(shortCut.Value.SettingKey, Path.Combine(tool.FullPath, shortCut.Value.RelativePath));
        }
        settingsService.Save(paths.SettingsPath);
        
        logger.Log($"Download {tool.Id} finished!");
    }
    
    private async Task DownloadNativeToolAsync(NativeTool tool)
    {
        await httpService.DownloadAndExtractArchiveAsync(tool.Url, Path.Combine(paths.NativeToolsDirectory, tool.Id));
    }
}