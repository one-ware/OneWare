namespace OneWare.Core.Extensions;

public static class HttpClientExtensions
{
    private const float ProgressReportStep = 0.005f;


    public static async Task DownloadAsync(this HttpClient client, string requestUri, Stream destination,
        IProgress<float>? progress = null, CancellationToken cancellationToken = default)
    {
        using var response =
            await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            var reason = response.ReasonPhrase;
            throw new HttpRequestException($"Download failed: {status} {reason}");
        }

        var contentLength = response.Content.Headers.ContentLength;

        await using var download = await response.Content.ReadAsStreamAsync(cancellationToken);

        // If progress is not required or content length is unknown, fallback to simple copy
        if (progress == null || !contentLength.HasValue)
        {
            await download.CopyToAsync(destination, cancellationToken);
            return;
        }

        var totalBytesRead = 0L;
        var lastReported = -1f;
        var buffer = new byte[81920];
        int bytesRead;

        while ((bytesRead = await download.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytesRead += bytesRead;

            // Network reads are small (~16KB), so reporting each one floods the UI thread with
            // tens of thousands of updates for large downloads. Only report noticeable changes.
            var current = (float)totalBytesRead / contentLength.Value;
            if (current - lastReported < ProgressReportStep) continue;
            lastReported = current;
            progress.Report(current);
        }

        // Ensure 100% progress is reported
        progress.Report(1f);
    }
}