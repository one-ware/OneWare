using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Services;

public class PackageDownloader : IPackageDownloader
{
    private readonly IHttpService _httpService;

    public PackageDownloader(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public Task<bool> DownloadAndExtractAsync(string url, string extractionPath, bool isArchive,
        IProgress<float> progress,
        CancellationToken cancellationToken = default)
    {
        if (isArchive)
            return _httpService.DownloadAndExtractArchiveAsync(url, extractionPath, progress,
                cancellationToken: cancellationToken);

        Directory.CreateDirectory(extractionPath);
        var fileName = Path.GetFileName(new Uri(url).LocalPath);
        return _httpService.DownloadFileAsync(url, Path.Combine(extractionPath, fileName), progress,
            cancellationToken: cancellationToken);
    }
}
