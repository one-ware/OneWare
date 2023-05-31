using OneWare.PackageManager.Internal;

namespace OneWare.PackageManager
{

    public static class Extensions
    {
        /// <summary>
        /// Checks for new version and performs an update if available.
        /// </summary>
        public static async Task CheckPerformUpdateAsync(this IDownloadManager manager, bool restart = true,
            IProgress<double> progress = null, CancellationToken cancellationToken = default)
        {
            manager.GuardNotNull(nameof(manager));

            // Check
            var result = await manager.CheckForUpdatesAsync();
            if (!result.CanUpdate)
                return;

            // Prepare
            await manager.PrepareUpdateAsync(result.LastVersion, progress, cancellationToken);

            // Apply
            manager.LaunchUpdater(result.LastVersion, restart);

            // Exit
            Environment.Exit(0);
        }
    }
}