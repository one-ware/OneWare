using System.Diagnostics;
using System.Runtime.InteropServices;
using OneWare.PackageManager.Exceptions;
using OneWare.PackageManager.Internal;
using OneWare.PackageManager.Models;
using OneWare.PackageManager.Services;

namespace OneWare.PackageManager
{
    /// <summary>
    /// Entry point for handling application updates.
    /// </summary>
    public class DownloadManager : IDownloadManager
    {
        private readonly string _packagePrefix;

        private readonly AssemblyMetadata _updatee;
        private readonly IPackageResolver _resolver;
        private readonly IPackageExtractor _extractor;

        private readonly string _storageDirPath;
        private readonly string _lockFilePath;

        private string _installerPath;
        private LockFile _lockFile;
        private bool _isDisposed;

        /// <summary>
        /// Initializes an instance of <see cref="DownloadManager"/>.
        /// </summary>
        public DownloadManager(AssemblyMetadata updatee, IPackageResolver resolver, IPackageExtractor extractor, string _packagePrefix)
        {
            this._packagePrefix = _packagePrefix;

            _updatee = updatee.GuardNotNull(nameof(updatee));
            _resolver = resolver.GuardNotNull(nameof(resolver));
            _extractor = extractor.GuardNotNull(nameof(extractor));

            // Set storage directory path
            _storageDirPath = Path.GetTempPath();

            // Set lock file path
            _lockFilePath = Path.Combine(_storageDirPath, "VHDPlus.lock");
        }

        /// <summary>
        /// Initializes an instance of <see cref="DownloadManager"/> on the entry assembly.
        /// </summary>
        public DownloadManager(IPackageResolver resolver, IPackageExtractor extractor, string packagePrefix)
            : this(AssemblyMetadata.FromEntryAssembly(), resolver, extractor, packagePrefix)
        {
        }

        private string GetPackageFilePath(Version version) => Path.Combine(_storageDirPath, $"{_packagePrefix}_{version}.vhdppack");

        private string GetPackageContentDirPath(Version version) => Path.Combine(_storageDirPath, $"{_packagePrefix}_{version}");

        private void EnsureNotDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        private void EnsureLockFileAcquired()
        {
            // Ensure storage directory exists
            Directory.CreateDirectory(_storageDirPath);

            // Try to acquire lock file if it's not acquired yet
            _lockFile ??= LockFile.TryAcquire(_lockFilePath);

            // If failed to acquire - throw
            if (_lockFile == null)
                throw new LockFileNotAcquiredException();
        }

        private void EnsureUpdaterNotLaunched()
        {
            // Check whether we have write access to updater executable
            // (this is a reasonably accurate check for whether that process is running)
            if (File.Exists(_installerPath) && !FileEx.CheckWriteAccess(_installerPath))
                throw new UpdaterAlreadyLaunchedException();
        }

        private void EnsureUpdatePrepared(Version version)
        {
            if (!IsUpdatePrepared(version))
                throw new UpdateNotPreparedException(version);
        }

        /// <inheritdoc />
        [NotNull]
        public async Task<CheckForUpdatesResult> CheckForUpdatesAsync(Version comparisonVersion = null)
        {
            // Ensure that the current state is valid for this operation
            EnsureNotDisposed();

            // Get versions
            var versions = await Task.Run(() => _resolver.GetPackageVersionsAsync());

            if (versions == null) return new CheckForUpdatesResult(null, null, false);
            
            var lastVersion = versions.Max();
            comparisonVersion ??= _updatee.Version;
            var canUpdate = lastVersion != null && comparisonVersion < lastVersion;

            return new CheckForUpdatesResult(versions, lastVersion, canUpdate);
        }

        /// <inheritdoc />
        public bool IsUpdatePrepared(Version version)
        {
            version.GuardNotNull(nameof(version));

            // Ensure that the current state is valid for this operation
            EnsureNotDisposed();

            // Package file should have been deleted after extraction
            // Updater file should exist
            return !string.IsNullOrEmpty(_installerPath) && File.Exists(_installerPath);
        }

        /// <inheritdoc />
        public async Task PrepareUpdateAsync(Version version,
            IProgress<double> progress = null, CancellationToken cancellationToken = default)
        {
            version.GuardNotNull(nameof(version));

            // Ensure that the current state is valid for this operation
            EnsureNotDisposed();
            EnsureLockFileAcquired();
            EnsureUpdaterNotLaunched();

            // Set up progress mixer
            var progressMixer = progress != null ? new ProgressMixer(progress) : null;

            // Get package file path and content directory path
            var packageFilePath = GetPackageFilePath(version);

            // Ensure storage directory exists
            Directory.CreateDirectory(_storageDirPath);

            // Download package
            var pkg = await _resolver.DownloadPackageAsync(version, packageFilePath,
                progressMixer?.Split(0.9), // 0% -> 90%
                cancellationToken);

            if (pkg == null) return;
            
            _installerPath = Path.Combine(_storageDirPath, Path.GetFileName(pkg.Url));
            
            File.Delete(_installerPath);
            
            File.Move(packageFilePath, _installerPath);
        }

        public async Task<PackageVersion> DownloadAndExtractPackage(Version version, string destination, 
            IProgress<double> progress = null, CancellationToken cancellationToken = default)
        {
            var progressMixer = progress != null ? new ProgressMixer(progress) : null;

            var packageFilePath = GetPackageFilePath(version);

            // Ensure storage directory exists
            Directory.CreateDirectory(_storageDirPath);

            // Download package
            var pkg = await _resolver.DownloadPackageAsync(version, packageFilePath,
                progressMixer?.Split(0.99), // 0% -> 90%
                cancellationToken);

            DirectoryEx.Reset(destination); //Delete package if it already exists

            // Extract package contents
            await _extractor.ExtractPackageAsync(packageFilePath, destination,
                progressMixer?.Split(0.01), // 90% -> 100%
                cancellationToken);

            // Delete package
            File.Delete(packageFilePath);

            return pkg;
        }

        public async Task<PackageVersion> DownloadFile(Version version, string destination,
            IProgress<double> progress = null, CancellationToken cancellationToken = default)
        {
            var packageFilePath = GetPackageFilePath(version);

            // Ensure storage directory exists
            Directory.CreateDirectory(_storageDirPath);

            // Download package
            var pkg = await _resolver.DownloadPackageAsync(version, packageFilePath,
                progress,
                cancellationToken);

            Directory.CreateDirectory(destination);

            File.Move(packageFilePath, Path.Combine(destination,Path.GetFileName(pkg.Url)));

            return pkg;
        }

        /// <inheritdoc />
        public void LaunchUpdater(Version version, bool restart = true)
        {
            version.GuardNotNull(nameof(version));

            // Ensure that the current state is valid for this operation
            EnsureNotDisposed();
            EnsureLockFileAcquired();
            EnsureUpdaterNotLaunched();
            EnsureUpdatePrepared(version);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "msiexec.exe",
                    Arguments = $"/i \"{_installerPath}\"",
                    Verb = "runas",
                };

                var updaterProcess = new Process { StartInfo = processStartInfo };
                updaterProcess.Start();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"hdiutil attach {_installerPath}\"",
                    UseShellExecute = true
                };

                var updaterProcess = new Process { StartInfo = processStartInfo };
                updaterProcess.Start();

                updaterProcess.WaitForExit();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _lockFile?.Dispose();
            }
        }
    }
}