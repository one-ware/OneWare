using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Copilot.Services;

/// <summary>
/// The <c>generate_image</c> agent tool. Generates images with the image models of OneWare Cloud
/// (<c>/api/copilot/v1/images/*</c>), billed in Compute Credits to the signed-in account's organization,
/// and saves them as files.
/// </summary>
internal static class OneWareCloudImageFunction
{
    public const string FunctionName = "generate_image";

    private const int MaxImages = 4;

    // Image generation at high quality regularly takes more than a minute.
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(10) };

    private static readonly string[] Sizes = ["auto", "1024x1024", "1536x1024", "1024x1536"];
    private static readonly string[] Qualities = ["auto", "low", "medium", "high"];

    public static void Register(IAiFunctionProvider functionProvider, IOneWareCloudAccess cloudAccess,
        IProjectExplorerService projectExplorerService)
    {
        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = FunctionName,
            FriendlyName = "Generate Image",
            Description =
                "Generates images from a text prompt with OneWare Cloud and saves them as files. Costs Compute " +
                "Credits of the user's OneWare Cloud organization, so only use it when the user asks for an image. " +
                "Returns the saved file paths; use readFile on a path to look at the result.",
            Handler = (
                    [Description("Detailed description of the image to generate.")] string prompt,
                    [Description("Where to save the image, e.g. assets/logo.png. Absolute or relative to the active project. With count > 1 a number is appended to the file name.")]
                    string path,
                    [Description("Image size: auto, 1024x1024, 1536x1024 (landscape) or 1024x1536 (portrait). Defaults to auto.")]
                    string? size = null,
                    [Description("Quality: auto, low, medium or high. Higher quality costs more. Defaults to auto.")]
                    string? quality = null,
                    [Description("Number of images to generate (1-4). Defaults to 1.")] int? count = null,
                    [Description("OneWare Cloud image model id. Omit to use the default model.")] string? model = null,
                    CancellationToken cancellationToken = default) =>
                GenerateAsync(cloudAccess, projectExplorerService, prompt, path, size, quality, count, model,
                    cancellationToken),
            DetailExtractor = args => GetString(args, "path"),
            ConfirmationCheck = args =>
            {
                var count = Math.Clamp(GetInt(args, "count") ?? 1, 1, MaxImages);
                var target = ResolvePath(projectExplorerService, GetString(args, "path")) ?? GetString(args, "path");
                var prompt = GetString(args, "prompt") ?? string.Empty;
                if (prompt.Length > 500) prompt = prompt[..500] + "…";
                return $"**Copilot wants to generate {(count == 1 ? "an image" : $"{count} images")} with OneWare Cloud.** " +
                       "This uses Compute Credits of your organization.\n\n" +
                       $"Save to: `{target ?? "?"}`\n\n> {prompt.ReplaceLineEndings(" ")}";
            }
        });
    }

    private static async Task<object> GenerateAsync(IOneWareCloudAccess cloudAccess,
        IProjectExplorerService projectExplorerService, string prompt, string path, string? size, string? quality,
        int? count, string? model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return Error("A prompt is required.");

        var target = ResolvePath(projectExplorerService, path);
        if (target is null)
            return Error("Pass an absolute path, or open a project to save the image relative to it.");

        var images = count ?? 1;
        if (images is < 1 or > MaxImages) return Error($"count must be between 1 and {MaxImages}.");
        if (size is not null && !Sizes.Contains(size, StringComparer.OrdinalIgnoreCase))
            return Error($"size must be one of: {string.Join(", ", Sizes)}.");
        if (quality is not null && !Qualities.Contains(quality, StringComparer.OrdinalIgnoreCase))
            return Error($"quality must be one of: {string.Join(", ", Qualities)}.");

        string token;
        try
        {
            token = await cloudAccess.GetAccessTokenAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Error("The user is not signed in to OneWare Cloud. Ask them to sign in (OneWare Agents chat or " +
                         "the account menu) and try again.");
        }

        var baseUrl = $"{cloudAccess.BaseUrl.TrimEnd('/')}/api/copilot/v1";

        if (string.IsNullOrWhiteSpace(model))
        {
            using var modelsRequest = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/images/models");
            modelsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var modelsResponse = await HttpClient.SendAsync(modelsRequest, cancellationToken);
            if (!modelsResponse.IsSuccessStatusCode)
                return Error(await DescribeErrorAsync(modelsResponse, cloudAccess, cancellationToken));

            var models = JsonNode.Parse(await modelsResponse.Content.ReadAsStringAsync(cancellationToken))?["data"]
                as JsonArray;
            model = models?.Select(item => item?["id"]?.GetValue<string>())
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
            if (model is null) return Error("OneWare Cloud does not offer an image model for this organization yet.");
        }

        var body = new JsonObject
        {
            ["model"] = model,
            ["prompt"] = prompt,
            ["n"] = images
        };
        if (size is not null && !size.Equals("auto", StringComparison.OrdinalIgnoreCase))
            body["size"] = size.ToLowerInvariant();
        if (quality is not null && !quality.Equals("auto", StringComparison.OrdinalIgnoreCase))
            body["quality"] = quality.ToLowerInvariant();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/images/generations")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return Error(await DescribeErrorAsync(response, cloudAccess, cancellationToken));

        var data = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))?["data"] as JsonArray;
        var saved = new List<string>();
        foreach (var item in data ?? [])
        {
            byte[] bytes;
            if (item?["b64_json"]?.GetValue<string>() is { Length: > 0 } base64)
                bytes = Convert.FromBase64String(base64);
            else if (item?["url"]?.GetValue<string>() is { Length: > 0 } url &&
                     Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                bytes = await HttpClient.GetByteArrayAsync(uri, cancellationToken);
            else
                continue;

            var file = WithExtension(NumberedPath(target, saved.Count, data!.Count), DetectExtension(bytes));
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllBytesAsync(file, bytes, cancellationToken);
            saved.Add(file);
        }

        if (saved.Count == 0) return Error("OneWare Cloud returned no image. The prompt may have been rejected.");

        return new
        {
            result = saved.Count == 1 ? $"Saved the image to {saved[0]}" : $"Saved {saved.Count} images.",
            files = saved,
            model,
            error = (string?)null
        };
    }

    /// <summary>Turns a OneWare Cloud error response into a message for the agent.</summary>
    private static async Task<string> DescribeErrorAsync(HttpResponseMessage response,
        IOneWareCloudAccess cloudAccess, CancellationToken cancellationToken)
    {
        string? message = null, code = null;
        try
        {
            var error = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))?["error"];
            message = error?["message"]?.GetValue<string>();
            code = error?["code"]?.GetValue<string>();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
        }

        var cloud = cloudAccess.BaseUrl.TrimEnd('/');
        return (response.StatusCode, code) switch
        {
            (HttpStatusCode.PaymentRequired, _) =>
                $"{message ?? "Not enough Compute Credits."} The user can add credits at {cloud}/credits.",
            (_, "cloud_ai_not_in_plan") =>
                $"{message ?? "Image generation is included in the OneWare Cloud Pro plans."} Upgrade at {cloud}/organization.",
            (_, "cloud_ai_disabled_by_organization") =>
                message ?? "OneWare Agents are disabled for this organization.",
            (HttpStatusCode.Unauthorized, _) =>
                "The OneWare Cloud session expired. Ask the user to sign in again.",
            _ => message ?? $"OneWare Cloud rejected the request ({(int)response.StatusCode} {response.ReasonPhrase})."
        };
    }

    private static object Error(string message) => new { result = (string?)null, error = message };

    private static string NumberedPath(string path, int index, int total)
    {
        if (total <= 1) return path;
        var directory = Path.GetDirectoryName(path)!;
        return Path.Combine(directory,
            $"{Path.GetFileNameWithoutExtension(path)}-{index + 1}{Path.GetExtension(path)}");
    }

    /// <summary>Keeps the requested extension when it matches the returned format, else uses the real one.</summary>
    private static string WithExtension(string path, string extension)
    {
        var current = Path.GetExtension(path).ToLowerInvariant();
        if (current == extension || (current == ".jpeg" && extension == ".jpg")) return path;
        return Path.ChangeExtension(path, extension);
    }

    private static string DetectExtension(byte[] bytes) => bytes switch
    {
        [0x89, 0x50, 0x4E, 0x47, ..] => ".png",
        [0xFF, 0xD8, 0xFF, ..] => ".jpg",
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => ".webp",
        _ => ".png"
    };

    private static string? ResolvePath(IProjectExplorerService projectExplorerService, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (Path.IsPathRooted(path)) return Path.GetFullPath(path);

        var root = projectExplorerService.ActiveProject?.FullPath;
        return string.IsNullOrWhiteSpace(root) ? null : Path.GetFullPath(Path.Combine(root, path));
    }

    private static string? GetString(AIFunctionArguments arguments, string name) =>
        arguments.TryGetValue(name, out var value) ? value switch
        {
            null => null,
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
            JsonElement json => json.ToString(),
            _ => value.ToString()
        } : null;

    private static int? GetInt(AIFunctionArguments arguments, string name) =>
        int.TryParse(GetString(arguments, name), out var value) ? value : null;
}
