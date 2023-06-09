namespace OneWare.Shared.Services;

public interface IHttpService
{
    public HttpClient HttpClient { get; }

    public Task<bool> DownloadFileAsync(string url, string location, IProgress<float>? progress, TimeSpan timeout = default,
        CancellationToken cancellationToken = default);

    public Task<bool> DownloadAndExtractArchiveAsync(string url, string location, IProgress<float>? progress = null,
        TimeSpan timeout = default, CancellationToken cancellationToken = default);
}