using System.Net.Http.Headers;
using System.Net.Sockets;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using Microsoft.Extensions.Logging;
using OneWare.Core.Extensions;
using OneWare.Essentials.Services;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace OneWare.Core.Services;

public class HttpService : IHttpService
{
    private readonly HttpClientHandler _handler;
    private readonly ILogger _logger;
    private readonly IPaths _paths;

    public HttpService(ILogger logger, IPaths paths)
    {
        _logger = logger;
        _paths = paths;
        _handler = new HttpClientHandler();
    }

    public HttpClient HttpClient => new(_handler);

    public async Task<bool> DownloadFileAsync(string url, Stream stream, IProgress<float>? progress = null,
        TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = HttpClient;
            if (timeout != default)
                client.Timeout = timeout;

            await client.DownloadAsync(url, stream, progress, cancellationToken);
            return true;
        }
        catch (HttpRequestException e)
        {
            LogOfflineOrUnexpected(e, url, "file");
        }
        catch (TaskCanceledException e)
        {
            LogOfflineOrUnexpected(e, url, "file");
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }

        return false;
    }

    public async Task<IImage?> DownloadImageAsync(string url, TimeSpan timeout = default,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = HttpClient;
            if (timeout != default)
                client.Timeout = timeout;

            using var download = await client.GetAsync(
                url, cancellationToken);

            var extension = Path.GetExtension(url);

            await using var stream = await download.Content.ReadAsStreamAsync(cancellationToken);

            switch (extension)
            {
                case ".svg":
                    var svg = SvgSource.LoadFromStream(stream);
                    if (svg is not null)
                        return new SvgImage
                        {
                            Source = svg
                        };
                    break;
                default:
                    return new Bitmap(stream);
            }
        }
        catch (HttpRequestException e)
        {
            LogOfflineOrUnexpected(e, url, "image");
        }
        catch (TaskCanceledException e)
        {
            LogOfflineOrUnexpected(e, url, "image");
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }

        return null;
    }

    public async Task<string?> DownloadTextAsync(string url, TimeSpan timeout = default,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = HttpClient;
            if (timeout != default)
                client.Timeout = timeout;

            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true
            };

            using var download = await client.GetAsync(
                url, cancellationToken);

            return await download.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException e)
        {
            LogOfflineOrUnexpected(e, url, "text");
        }
        catch (TaskCanceledException e)
        {
            LogOfflineOrUnexpected(e, url, "text");
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }

        return null;
    }

    public async Task<bool> DownloadFileAsync(string url, string location, IProgress<float>? progress = null,
        TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var file = new FileStream(location, FileMode.Create, FileAccess.Write, FileShare.None);
            return await DownloadFileAsync(url, file, progress, timeout, cancellationToken);
        }
        catch (HttpRequestException e)
        {
            LogOfflineOrUnexpected(e, url, "file");
        }
        catch (TaskCanceledException e)
        {
            LogOfflineOrUnexpected(e, url, "file");
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }

        return false;
    }

    public async Task<bool> DownloadAndExtractArchiveAsync(string url, string location,
        IProgress<float>? progress = null, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        var tempPath = Path.Combine(_paths.TempDirectory, Path.GetFileName(url));

        try
        {
            Directory.CreateDirectory(location);

            if (!await DownloadFileAsync(url, tempPath, progress, timeout, cancellationToken))
                return false;

            await Task.Run(() =>
            {
                using var stream = File.OpenRead(tempPath);
                var reader = ReaderFactory.OpenReader(stream);
                while (reader.MoveToNextEntry())
                    if (!reader.Entry.IsDirectory)
                        reader.WriteEntryToDirectory(location,
                            new ExtractionOptions
                            {
                                ExtractFullPath = true, Overwrite = true, 
                                SymbolicLinkHandler = (path, targetPath) => { File.CreateSymbolicLink(path, targetPath); }
                            });
            }, cancellationToken);

            File.Delete(tempPath);
            return true;
        }
        catch (HttpRequestException e)
        {
            LogOfflineOrUnexpected(e, url, "archive");
        }
        catch (TaskCanceledException e)
        {
            LogOfflineOrUnexpected(e, url, "archive");
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }

        return false;
    }

    private void LogOfflineOrUnexpected(Exception exception, string url, string contentType)
    {
        // Offline/timeout cases are expected and should not show user-visible warnings.
        if (IsConnectivityIssue(exception))
        {
            _logger.LogDebug(exception, "Skipping {ContentType} download while offline: {Url}", contentType, url);
            return;
        }

        _logger.Warning(exception.Message, exception);
    }

    private static bool IsConnectivityIssue(Exception exception)
    {
        if (exception is TaskCanceledException)
            return true;

        if (exception is HttpRequestException { InnerException: SocketException })
            return true;

        if (exception is HttpRequestException { InnerException: IOException })
            return true;

        var message = exception.Message;
        return message.Contains("Name or service not known", StringComparison.OrdinalIgnoreCase)
               || message.Contains("No such host is known", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Network is unreachable", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Operation timed out", StringComparison.OrdinalIgnoreCase)
               || message.Contains("The operation was canceled", StringComparison.OrdinalIgnoreCase);
    }
}
