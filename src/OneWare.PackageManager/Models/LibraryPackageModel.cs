using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using Microsoft.Extensions.Logging;

namespace OneWare.PackageManager.Models;

public class LibraryPackageModel(
    Package package,
    IHttpService httpService,
    ILogger logger,
    IPaths paths,
    IApplicationStateService applicationStateService)
    : PackageModel(package, "Libraries", Path.Combine(paths.PackagesDirectory, "Libraries", package.Id!), httpService, logger,
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