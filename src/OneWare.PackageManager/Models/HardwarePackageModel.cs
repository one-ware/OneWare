using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging; // Ensure this is the correct namespace
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Models
{
    public class HardwarePackageModel : PackageModel
    {
        public HardwarePackageModel(
            Package package,
            IHttpService httpService,
            ILogger<PackageModel> logger, // Ensure this is ILogger<PackageModel>
            IPaths paths,
            IApplicationStateService applicationStateService,
            PlatformHelper platformHelper)
            : base(package, "Hardware", Path.Combine(paths.PackagesDirectory, "Hardware", package.Id!), httpService, logger, applicationStateService, platformHelper)
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
