using Avalonia.Media.Imaging;

namespace OneWare.Shared.Services;

public interface IHttpService
{
    public HttpClient HttpClient { get; }

    public Task<Bitmap?> DownloadImageAsync(string url, TimeSpan timeout = default,
        CancellationToken cancellationToken = default);

    public Task<bool> DownloadFileAsync(string url, Stream stream, IProgress<float>? progress = null,
        TimeSpan timeout = default, CancellationToken cancellationToken = default);
    
    public Task<bool> DownloadFileAsync(string url, string location, IProgress<float>? progress = null, TimeSpan timeout = default,
        CancellationToken cancellationToken = default);

    public Task<bool> DownloadAndExtractArchiveAsync(string url, string location, IProgress<float>? progress = null,
        TimeSpan timeout = default, CancellationToken cancellationToken = default);
}