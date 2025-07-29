using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Models;

public class LibraryPackageModel(
    Package package,
    IHttpService httpService,
    IPaths paths,
    IApplicationStateService applicationStateService)
    : PackageModel(package, "Libraries", Path.Combine(paths.PackagesDirectory, "Libraries", package.Id!), httpService,
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