using OneWare.PackageManager.Models;
using OneWare.PackageManager.Services;

namespace OneWare.PackageManager
{
    /// <summary>
    /// Interface for <see cref="DownloadManager"/>.
    /// </summary>
    public interface IDownloadManager : IDisposable
    {
        /// <summary>
        /// Checks for updates.
        /// </summary>
        Task<CheckForUpdatesResult> CheckForUpdatesAsync(Version comparison = null);

        /// <summary>
        /// Checks whether an update to given version has been prepared.
        /// </summary>
        bool IsUpdatePrepared(Version version);

        /// <summary>
        /// Prepares an update to given version.
        /// </summary>
        Task PrepareUpdateAsync(Version version,
            IProgress<double> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Just download and extract
        /// </summary>
        Task<PackageVersion> DownloadAndExtractPackage(Version version, string destination,
            IProgress<double> progress = null, CancellationToken cancellationToken = default);

        Task<PackageVersion> DownloadFile(Version version, string destination,
            IProgress<double> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Launches an external executable that will apply an update to given version, once this application exits.
        /// </summary>
        void LaunchUpdater(Version version, bool restart = true);
    }
}