using System;
using System.IO;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Installers;

public abstract class PackageInstallerBase : IPackageInstaller
{
    public virtual string GetExtractionPath(Package package, IPaths paths)
    {
        if (package.Id == null) throw new InvalidOperationException("Package Id is required.");

        return package.Type switch
        {
            "Plugin" => Path.Combine(paths.PluginsDirectory, package.Id),
            "NativeTool" => Path.Combine(paths.NativeToolsDirectory, package.Id),
            "OnnxRuntime" => Path.Combine(paths.OnnxRuntimesDirectory, package.Id),
            "Hardware" => Path.Combine(paths.PackagesDirectory, "Hardware", package.Id),
            "Library" => Path.Combine(paths.PackagesDirectory, "Libraries", package.Id),
            "Onnx" => Path.Combine(paths.PackagesDirectory, "Onnx", package.Id),
            _ => Path.Combine(paths.PackagesDirectory, package.Id)
        };
    }

    public virtual PackageTarget? SelectTarget(Package package, PackageVersion version)
    {
        var currentTarget = PlatformHelper.Platform.ToString().ToLower();
        return version.Targets?.FirstOrDefault(x => x.Target?.Replace("-", "") == currentTarget)
               ?? version.Targets?.FirstOrDefault(x => x.Target == "all");
    }

    public virtual Task<CompatibilityReport> CheckCompatibilityAsync(Package package, PackageVersion version,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CompatibilityReport(true));
    }

    public virtual Task PrepareRemoveAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public abstract Task<PackageInstallerResult> InstallAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default);

    public abstract Task<PackageInstallerResult> RemoveAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default);
}
