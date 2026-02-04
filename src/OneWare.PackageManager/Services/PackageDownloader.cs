using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Services;

public class PackageDownloader : IPackageDownloader
{
    private readonly IHttpService _httpService;

    public PackageDownloader(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public Task<bool> DownloadAndExtractAsync(string url, string extractionPath, IProgress<float> progress,
        CancellationToken cancellationToken = default)
    {
        return _httpService.DownloadAndExtractArchiveAsync(url, extractionPath, progress);
    }
}
