using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.Converters;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.Updater.Views;
using Prism.Ioc;

namespace OneWare.Updater.ViewModels;

public enum UpdaterStatus
{
    UpdateUnavailable,
    UpdateAvailable,
    Installing,
    RestartRequired
}

public class UpdaterViewModel : ObservableObject
{
    private readonly IHttpService _httpService;
    private readonly IPaths _paths;
    private readonly ILogger _logger;
    private readonly IApplicationStateService _applicationStateService;
    
    private UpdaterStatus _status = UpdaterStatus.UpdateUnavailable;


    public Version CurrentVersion => Assembly.GetEntryAssembly()!.GetName().Version!;

    public Version? NewVersion { get; private set; }

    public string VersionInfo => $"{_paths.AppName} {CurrentVersion.ToString()}";

    private int _progress;

    public int Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public UpdaterViewModel(IHttpService httpService, IPaths paths, ILogger logger,
        IApplicationStateService applicationStateService)
    {
        _httpService = httpService;
        _paths = paths;
        _logger = logger;
        _applicationStateService = applicationStateService;
        
        applicationStateService.RegisterShutdownAction(PerformRestartAction);
    }

    public UpdaterStatus Status
    {
        get => _status;
        set
        {
            SetProperty(ref _status, value);
            OnPropertyChanged(nameof(UpdateMessage));
        }
    }

    public string UpdateMessage => Status switch
    {
        UpdaterStatus.UpdateAvailable => $"Update {NewVersion} available!",
        UpdaterStatus.Installing => "Downloading update...",
        UpdaterStatus.RestartRequired => "Download finished. Restart to install.",
        _ => "No updates available."
    };

    public string? DownloadLink { get; private set; }

    private string DownloadLocation => PlatformHelper.Platform switch
    {
        PlatformId.WinX64 or PlatformId.WinArm64 => Path.Combine(_paths.TempDirectory, $"{_paths.AppName}_{NewVersion}.msi"),
        PlatformId.OsxX64 or PlatformId.OsxArm64 => Path.Combine(_paths.TempDirectory,  $"{_paths.AppName}_{NewVersion}.dmg"),
        _ => Path.Combine(_paths.TempDirectory,  $"{_paths.AppName}_{NewVersion}.zip"),
    };

    public async Task<bool> CheckForUpdateAsync()
    {
        var versionStr = PlatformHelper.Platform switch
        {
            PlatformId.WinX64 => "win-x64",
            PlatformId.WinArm64 => "win-arm64",
            PlatformId.OsxArm64 => "osx-arm64",
            PlatformId.OsxX64 => "osx-x64",
            _ => null
        };

        if (versionStr == null) return false;

        var updateLink = $"{_paths.UpdateInfoUrl}/{versionStr}.txt";
        var updateText = await _httpService.DownloadTextAsync(updateLink);

        if (updateText != null)
        {
            var parts = updateText.Split('|');
            if (parts.Length == 2)
            {
                var version = Version.Parse(parts[0]);
                DownloadLink = parts[1];

                if (version > Assembly.GetEntryAssembly()!.GetName().Version)
                {
                    NewVersion = version;
                    Status = UpdaterStatus.UpdateAvailable;
                    return true;
                }
                else
                {
                    NewVersion = null;
                    Status = UpdaterStatus.UpdateUnavailable;
                }
            }
        }

        return false;
    }

    public async Task DownloadUpdateAsync()
    {
        if (DownloadLink == null) return;

        Status = UpdaterStatus.Installing;

        var result = await _httpService.DownloadFileAsync(DownloadLink, DownloadLocation,
            new Progress<float>(x => Progress = (int)(x * 100)));

        if (!result)
        {
            Status = UpdaterStatus.UpdateAvailable;
            return;
        }

        Status = UpdaterStatus.RestartRequired;
    }

    public void TryRestart()
    {
        _applicationStateService.TryShutdown();
    }

    private void PerformRestartAction()
    {
        if (Status is not UpdaterStatus.RestartRequired) return;

        if (!File.Exists(DownloadLocation))
        {
            _logger.Error($"Update file not found at {DownloadLocation}!");
            return;
        }

        switch (PlatformHelper.Platform)
        {
            case PlatformId.WinX64 or PlatformId.WinArm64:
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "msiexec",
                        Arguments = $"/i {DownloadLocation}",
                        UseShellExecute = true
                    }
                };
                process.Start();
                break;
            }
            case PlatformId.OsxX64 or PlatformId.OsxArm64:
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "open",
                        Arguments = DownloadLocation,
                        UseShellExecute = true
                    }
                };
                process.Start();
                break;
            }
        }
    }
}