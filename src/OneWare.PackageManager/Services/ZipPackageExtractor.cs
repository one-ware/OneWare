using SharpCompress.Common;
using SharpCompress.Readers;
using OneWare.PackageManager.Internal;

namespace OneWare.PackageManager.Services
{
    /// <summary>
    /// Extracts files from zip-archived packages.
    /// </summary>
    public class ZipPackageExtractor : IPackageExtractor
    {
        /// <inheritdoc />
        public async Task ExtractPackageAsync(string sourceFilePath, string destDirPath,
            IProgress<double> progress = null, CancellationToken cancellationToken = default)
        {
            sourceFilePath.GuardNotNull(nameof(sourceFilePath));
            destDirPath.GuardNotNull(nameof(destDirPath));

            await Task.Run(() =>
            {
                using (Stream stream = File.OpenRead(sourceFilePath))
                {
                    var reader = ReaderFactory.Open(stream);
                    while (reader.MoveToNextEntry())
                    {
                        if (!reader.Entry.IsDirectory)
                        {
                            reader.WriteEntryToDirectory(destDirPath, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                        }
                    }
                }
            });
        }
    }
}