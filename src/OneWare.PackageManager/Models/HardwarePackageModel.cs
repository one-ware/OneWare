using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Models;

public class HardwarePackageModel(
    Package package,
    IHttpService httpService,
    ILogger logger,
    IPaths paths,
    IApplicationStateService applicationStateService)
    : PackageModel(package, "Hardware", Path.Combine(paths.PackagesDirectory, "Hardware", package.Id!), httpService, logger,
        applicationStateService)
{
    protected override void Install(PackageTarget target)
    {
        Status = PackageStatus.Installed;
    }

    protected override void Uninstall()
    {
        Status = PackageStatus.Available;
    }
}