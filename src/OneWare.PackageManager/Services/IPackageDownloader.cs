namespace OneWare.PackageManager.Services;

public interface IPackageDownloader
{
    Task<bool> DownloadAndExtractAsync(string url, string extractionPath, bool isArchive, IProgress<float> progress,
        IProgress<float>? extractProgress = null, CancellationToken cancellationToken = default);
}
