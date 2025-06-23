using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Models
{
    public class LibraryPackageModel : PackageModel
    {
        public LibraryPackageModel(
            Package package,
            IHttpService httpService,
            ILogger<PackageModel> logger,
            IPaths paths,
            IApplicationStateService applicationStateService,
            PlatformHelper platformHelper) // Include PlatformHelper as a parameter
            : base(package, "Libraries", Path.Combine(paths.PackagesDirectory, "Libraries", package.Id!), httpService, logger, applicationStateService, platformHelper) // Pass platformHelper to the base constructor
        {
        }

        protected override void Install(PackageTarget target)
        {
            Status = PackageStatus.Installed;
        }

        protected override void Uninstall()
        {
            Status = PackageStatus.Available;
        }
    }
}
