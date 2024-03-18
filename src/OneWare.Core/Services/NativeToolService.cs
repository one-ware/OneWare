using Avalonia.Media;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.NativeTools;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class NativeToolService(IHttpService httpService, ISettingsService settingsService, IPaths paths, IApplicationStateService applicationStateService, ILogger logger) : INativeToolService
{
    private readonly Dictionary<string, NativeToolContainer> _nativeTools = new();
    
    private readonly Dictionary<NativeToolContainer, Task<bool>> _activeInstalls = new();
    
    private readonly object _lock = new();
    
    public NativeToolContainer Register(string key, Version version)
    {
        _nativeTools[key] = new NativeToolContainer(key, Path.Combine(paths.NativeToolsDirectory, key), version);
        return _nativeTools[key];
    }

    public NativeToolContainer? Get(string key)
    {
        return _nativeTools.GetValueOrDefault(key);
    }

    public Task<bool> InstallAsync(NativeToolContainer container)
    {
        lock (_lock)
        {
            if (_activeInstalls.TryGetValue(container, out var task))
            {
                if (!task.IsCompleted)
                    return task;
                _activeInstalls.Remove(container);
            }
            var newTask = PerformInstallAsync(container);
            _activeInstalls.Add(container, newTask);
            return newTask;
        }
    }
    
    private async Task<bool> PerformInstallAsync(NativeToolContainer container)
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
    
    private async Task<bool> DownloadNativeToolAsync(NativeTool tool, IProgress<float> progress)
    {
        var result = await httpService.DownloadAndExtractArchiveAsync(tool.Url, tool.FullPath, progress);
        if (!result) return false;
        PlatformHelper.ChmodFolder(tool.FullPath);
        return result;
    }
}