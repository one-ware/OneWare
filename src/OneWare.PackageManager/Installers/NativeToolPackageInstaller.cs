using System;
using System.IO;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Installers;

public class NativeToolPackageInstaller : PackageInstallerBase
{
    /// <summary>
    /// How long the operating system gets to release the tool's files after its processes were
    /// killed.
    /// </summary>
    private static readonly TimeSpan ReleaseTimeout = TimeSpan.FromSeconds(5);

    private readonly IChildProcessService _childProcessService;
    private readonly IPaths _paths;
    private readonly ISettingsService _settingsService;

    public NativeToolPackageInstaller(ISettingsService settingsService, IPaths paths,
        IChildProcessService childProcessService)
    {
        _settingsService = settingsService;
        _paths = paths;
        _childProcessService = childProcessService;
    }
    
    public override string GetExtractionPath(Package package, IPaths paths)
    {
        if (package.Id == null) throw new InvalidOperationException("Package Id is required.");
        return Path.Combine(paths.NativeToolsDirectory, package.Id);
    }

    public override PackageTarget? SelectTarget(Package package, PackageVersion version)
    {
        var target = base.SelectTarget(package, version);

        if (target == null && PlatformHelper.Platform is PlatformId.OsxArm64)
            target = version.Targets?.FirstOrDefault(x => x.Target == "osx-x64");

        return target;
    }

    public override Task<PackageInstallerResult> InstallAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Target.AutoSetting == null)
            return Task.FromResult(new PackageInstallerResult(PackageStatus.Installed));

        foreach (var shortCut in context.Target.AutoSetting)
            if (shortCut is { RelativePath: not null, SettingKey: not null })
            {
                var fullPath = Path.Combine(context.ExtractionPath, shortCut.RelativePath);
                _settingsService.SetSettingValue(shortCut.SettingKey, "");
                _settingsService.SetSettingValue(shortCut.SettingKey, fullPath);
                _settingsService.Save(_paths.SettingsPath);
            }

        return Task.FromResult(new PackageInstallerResult(PackageStatus.Installed));
    }

    /// <summary>
    /// Stops everything that still runs from the installation directory, so it can be deleted.
    /// A running native tool keeps its own executable open, which makes deleting or overwriting it
    /// fail ("Text file busy" on Unix, sharing violation on Windows). Processes tracked by OneWare
    /// are stopped first, then anything else running from the directory - a tool started through
    /// an SDK for example. Files held by a process that cannot be killed, such as a tool used by a
    /// second OneWare instance, are unlinked instead; that process keeps the old file open.
    /// </summary>
    public override async Task PrepareRemoveAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(context.ExtractionPath)) return;

        // Tracked processes are keyed by their executable path, so they have to be looked up per
        // shortcut instead of by the installation directory.
        foreach (var shortCut in context.Target.AutoSetting ?? [])
        {
            if (shortCut.RelativePath == null) continue;

            var fullPath = Path.Combine(context.ExtractionPath, shortCut.RelativePath);

            foreach (var process in _childProcessService.GetChildProcesses(fullPath).ToArray())
                _childProcessService.Kill(process);
        }

        if (await ProcessHelper.ReleaseDirectoryAsync(context.ExtractionPath, ReleaseTimeout, cancellationToken))
            return;

        ProcessHelper.FreeBusyFiles(context.ExtractionPath);
    }

    public override Task<PackageInstallerResult> RemoveAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PackageInstallerResult(PackageStatus.Available));
    }
}
