using Avalonia.Media;
using OneWare.SDK.Enums;
using OneWare.SDK.Helpers;
using OneWare.SDK.NativeTools;
using OneWare.SDK.Services;

namespace OneWare.Core.Services;

public class NativeToolService(IHttpService httpService, ISettingsService settingsService, IPaths paths, IApplicationStateService applicationStateService, ILogger logger) : INativeToolService
{
    private readonly Dictionary<string, NativeToolContainer> _nativeTools = new();
    
    public NativeToolContainer Register(string key)
    {
        _nativeTools[key] = new NativeToolContainer(key, Path.Combine(paths.NativeToolsDirectory, key));
        return _nativeTools[key];
    }

    public NativeToolContainer? Get(string key)
    {
        return _nativeTools.GetValueOrDefault(key);
    }

    public async Task<bool> InstallAsync(NativeToolContainer container)
    {
        var tool = container.GetPlatform();

        if (tool == null)
        {
            logger.Warning($"Tool {container.Id} currently not supported for {PlatformHelper.Platform.ToString()}");
            return false;
        }
        
        logger.Log($"Downloading {container.Id} for {PlatformHelper.Platform.ToString()}...", ConsoleColor.Gray, true, Brushes.Gray);

        var state = applicationStateService.AddState($"Downloading {container.Id}...", AppState.Loading);

        bool success;
        
        try
        {
            var progress = new Progress<float>(x =>
            {
                state.StatusMessage = $"Downloading {container.Id} {(int)(x*100)}%";
            });
            success = await DownloadNativeToolAsync(tool, progress);

            foreach (var shortCut in tool.ShortCuts)
            {
                if(shortCut.Value.SettingKey != null)
                    settingsService.SetSettingValue(shortCut.Value.SettingKey, Path.Combine(tool.FullPath, shortCut.Value.RelativePath));
            }
            settingsService.Save(paths.SettingsPath);
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
            return false;
        }
        
        applicationStateService.RemoveState(state);

        if (success)
        {
            logger.Log($"Download {container.Id} finished!", ConsoleColor.Gray, true, Brushes.Gray);
            return true;
        }
        logger.Warning($"Download {container.Id} failed!");
        return false;
    }
    
    private Task<bool> DownloadNativeToolAsync(NativeTool tool, IProgress<float> progress)
    {
        return httpService.DownloadAndExtractArchiveAsync(tool.Url, tool.FullPath, progress);
    }
}