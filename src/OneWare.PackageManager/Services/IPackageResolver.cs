namespace OneWare.PackageManager.Services
{
    /// <summary>
    /// Provider for resolving packages.
    /// </summary>
    public interface IPackageResolver
    {
        /// <summary>
        /// Gets all available package versions.
        /// </summary>
        Task<IReadOnlyList<Version>> GetPackageVersionsAsync();

        /// <summary>
        /// Downloads given package version.
        /// </summary>
        Task<PackageVersion> DownloadPackageAsync(Version version, string destFilePath,
            IProgress<double> progress = null, CancellationToken cancellationToken = default);
    }
}