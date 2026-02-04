using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Updater.ViewModels;

public enum UpdaterStatus
{
    UpdateUnavailable,
    UpdateAvailable,
    UpdatingPackages,
    Installing,
    RestartRequired
}

public class UpdaterViewModel : ObservableObject
{
    private readonly IApplicationStateService _applicationStateService;
    private readonly IHttpService _httpService;
    private readonly ILogger _logger;
    private readonly IPackageManager _packageService;
    private readonly IPaths _paths;
    private readonly IWindowService _windowService;

    private int _progress;

    private UpdaterStatus _status = UpdaterStatus.UpdateUnavailable;

    public UpdaterViewModel(IHttpService httpService, IPaths paths, ILogger logger,
        IApplicationStateService applicationStateService, IWindowService windowService, IPackageManager packageService)
    {
        _httpService = httpService;
        _paths = paths;
        _logger = logger;
        _applicationStateService = applicationStateService;
        _packageService = packageService;
        _windowService = windowService;

        applicationStateService.RegisterShutdownAction(OpenUpdaterAction);
    }


    public Version CurrentVersion => Assembly.GetEntryAssembly()!.GetName().Version!;

    public Version? NewVersion { get; private set; }

    public string VersionInfo => $"{_paths.AppName} {CurrentVersion.ToString()}";

    public int Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public bool IsIndeterminate => Status switch
    {
        UpdaterStatus.UpdatingPackages => true,
        _ => false
    };

    public UpdaterStatus Status
    {
        get => _status;
        set
        {
            SetProperty(ref _status, value);
            OnPropertyChanged(nameof(UpdateMessage));
            OnPropertyChanged(nameof(IsIndeterminate));
        }
    }

    public string UpdateMessage => Status switch
    {
        UpdaterStatus.UpdateAvailable => $"Update {NewVersion} available!",
        UpdaterStatus.Installing => "Downloading update...",
        UpdaterStatus.RestartRequired => "Download finished. Restart to install.",
        UpdaterStatus.UpdatingPackages => "Updating packages...",
        _ => "No updates available."
    };

    public string? DownloadLink { get; private set; }

    private string DownloadLocation => PlatformHelper.Platform switch
    {
        PlatformId.WinX64 or PlatformId.WinArm64 => Path.Combine(_paths.TempDirectory,
            $"{_paths.AppName.Replace(" ", "")}_{NewVersion}.msi"),
        PlatformId.OsxX64 or PlatformId.OsxArm64 => Path.Combine(_paths.TempDirectory,
            $"{_paths.AppName.Replace(" ", "")}_{NewVersion}.dmg"),
        _ => Path.Combine(_paths.TempDirectory, $"{_paths.AppName}_{NewVersion}.zip")
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

                NewVersion = null;
                Status = UpdaterStatus.UpdateUnavailable;
            }
        }

        return false;
    }

    public async Task DownloadUpdateAsync(Control owner)
    {
        if (DownloadLink == null || NewVersion == null) return;
        var topLevelWindow = TopLevel.GetTopLevel(owner) as Window;

        Status = UpdaterStatus.UpdatingPackages;

        var loadPackages = await _packageService.RefreshAsync();

        if (!loadPackages)
        {
            var resultContinue = await _windowService.ShowYesNoAsync("Warning", "Loading package updates failed",
                MessageBoxIcon.Warning, topLevelWindow);

            if (resultContinue != MessageBoxStatus.Yes)
            {
                Status = UpdaterStatus.UpdateAvailable;
                return;
            }
        }

        var updatablePackages = _packageService.Packages
            .Select(x => x.Value)
            .Where(x => x.Status == PackageStatus.UpdateAvailable)
            .ToArray();

        if (updatablePackages.Length > 0)
        {
            var updateString = string.Join('\n',
                updatablePackages.Select(x => x.Package.Name + " -> " + x.Package.Versions!.Last().Version).ToArray());

            var resultContinue = await _windowService.ShowYesNoCancelAsync("Update Packages",
                $"There are package updates available:\n{updateString}\nDo you want to update them now?",
                MessageBoxIcon.Warning, topLevelWindow);

            if (resultContinue == MessageBoxStatus.Canceled)
            {
                Status = UpdaterStatus.UpdateAvailable;
                return;
            }

            if (resultContinue == MessageBoxStatus.Yes)
            {
            var updateTasks = updatablePackages
                .Select(x => _packageService.UpdateAsync(x.Package.Id!, x.Package.Versions!.Last(),
                    includePrerelease: true, ignoreCompatibility: true));

                var updateResult = await Task.WhenAll(updateTasks);

                if (!updateResult.All(x => true))
                {
                    _logger.Error("At least one package update have failed", null, true, true, topLevelWindow);
                    Status = UpdaterStatus.UpdateAvailable;
                    return;
                }
            }
        }

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
        _ = _applicationStateService.TryShutdownAsync();
    }

    private void OpenUpdaterAction()
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
                        Arguments = $"/i \"{DownloadLocation}\"",
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
                        Arguments = $"\"{DownloadLocation}\"",
                        UseShellExecute = true
                    }
                };
                process.Start();
                break;
            }
        }
    }
}
