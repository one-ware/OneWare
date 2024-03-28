using System.Diagnostics;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using OneWare.Core.Extensions;
using OneWare.Essentials.Services;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace OneWare.Core.Services;

public class HttpService : IHttpService
{
    private readonly IPaths _paths;
    private readonly ILogger _logger;

    private readonly HttpClientHandler _handler;
    public HttpClient HttpClient => new(_handler);

    public HttpService(ILogger logger, IPaths paths)
    {
        _logger = logger;
        _paths = paths;
        _handler = new HttpClientHandler();
    }

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
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return false;
        }
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

            switch (extension)
            {
                case ".svg":
                    var svg = new SvgSource();
                    var picture = svg.Load(await download.Content.ReadAsStreamAsync(cancellationToken));
                    if (picture is not null)
                    {
                        return new SvgImage()
                        {
                            Source = svg
                        };
                    }
                    break;
                default:
                    return new Bitmap(await download.Content.ReadAsStreamAsync(cancellationToken));
            }
        }
        catch (HttpRequestException e)
        {
            _logger.Log(e, ConsoleColor.Yellow);
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

            using var download = await client.GetAsync(
                url, cancellationToken);

            return await download.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException e)
        {
            _logger.Log(e, ConsoleColor.Yellow);
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
            _logger.Log(e, ConsoleColor.Yellow);
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

        //if (Directory.Exists(location))
        //{
        //    _logger.Error("Destination dir already exists");
        //    return false;
        //}

        try
        {
            Directory.CreateDirectory(location);

            if (!await DownloadFileAsync(url, tempPath, progress, timeout, cancellationToken))
                throw new Exception("Download failed");

            await Task.Run(() =>
            {
                using var stream = File.OpenRead(tempPath);
                var reader = ReaderFactory.Open(stream);
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        reader.WriteEntryToDirectory(location,
                            new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                    }
                }
            }, cancellationToken);

            File.Delete(tempPath);
            return true;
        }
        catch (Exception e)
        {
            if(e is not HttpRequestException)
                _logger.Error(e.Message, e);
            else _logger.Log(e.Message, ConsoleColor.Yellow);
            try
            {
                Directory.Delete(location);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return false;
        }
    }
}