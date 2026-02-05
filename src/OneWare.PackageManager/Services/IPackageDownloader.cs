namespace OneWare.PackageManager.Services;

public interface IPackageDownloader
{
    Task<bool> DownloadAndExtractAsync(string url, string extractionPath, IProgress<float> progress,
        CancellationToken cancellationToken = default);
}
