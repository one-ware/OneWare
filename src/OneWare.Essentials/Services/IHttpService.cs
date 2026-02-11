using Avalonia.Media;

namespace OneWare.Essentials.Services;

public interface IHttpService
{
    /// <summary>
    /// Shared HTTP client instance.
    /// </summary>
    public HttpClient HttpClient { get; }

    /// <summary>
    /// Downloads a file into a stream.
    /// </summary>
    public Task<bool> DownloadFileAsync(string url, Stream stream, IProgress<float>? progress = null,
        TimeSpan timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file to a location on disk.
    /// </summary>
    public Task<bool> DownloadFileAsync(string url, string location, IProgress<float>? progress = null,
        TimeSpan timeout = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads and extracts an archive to a location on disk.
    /// </summary>
    public Task<bool> DownloadAndExtractArchiveAsync(string url, string location, IProgress<float>? progress = null,
        TimeSpan timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an image and returns it.
    /// </summary>
    public Task<IImage?> DownloadImageAsync(string url, TimeSpan timeout = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads text content.
    /// </summary>
    public Task<string?> DownloadTextAsync(string url, TimeSpan timeout = default,
        CancellationToken cancellationToken = default);
}
