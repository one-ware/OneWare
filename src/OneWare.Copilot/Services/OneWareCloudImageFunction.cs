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
/// The <c>list_image_models</c> and <c>generate_image</c> agent tools. Generates images with the image models of
/// OneWare Cloud (<c>/api/copilot/v1/images/*</c>), billed in Compute Credits to the signed-in account's
/// organization, and saves them as files.
/// </summary>
internal static class OneWareCloudImageFunction
{
    public const string FunctionName = "generate_image";
    public const string ListFunctionName = "list_image_models";

    private const int MaxImages = 4;

    // Image generation at high quality regularly takes more than a minute.
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(10) };

    private static readonly TimeSpan ModelListTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ModelCacheDuration = TimeSpan.FromMinutes(5);

    // Used when the server does not advertise the options of a model (older OneWare Cloud versions).
    private static readonly string[] FallbackSizes = ["auto", "1024x1024", "1536x1024", "1024x1536"];
    private static readonly string[] FallbackQualities = ["auto", "low", "medium", "high"];

    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private static ModelCache? _cache;
    private static IDisposable? _accountSubscription;

    public static void Register(IAiFunctionProvider functionProvider, IOneWareCloudAccess cloudAccess,
        IProjectExplorerService projectExplorerService)
    {
        _accountSubscription?.Dispose();
        _accountSubscription = cloudAccess.UserIdObservable.Subscribe(_ => _cache = null);

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = ListFunctionName,
            FriendlyName = "List Image Models",
            IsReadOnly = true,
            Description =
                "Checks whether OneWare Cloud image generation is available and lists the image models the user's " +
                "organization may use, with their supported sizes, qualities and image limits. Free; call it " +
                $"before proposing or calling {FunctionName}. When 'available' is false, tell the user the " +
                "'reason' instead of trying to generate.",
            Handler = (CancellationToken cancellationToken = default) =>
                ListModelsAsync(cloudAccess, cancellationToken)
        });

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = FunctionName,
            FriendlyName = "Generate Image",
            Description =
                "Generates images from a text prompt with OneWare Cloud and saves them as files. Costs Compute " +
                "Credits of the user's OneWare Cloud organization, so only use it when the user asks for an image. " +
                $"Call {ListFunctionName} first to check availability and to pick a model, size and quality the " +
                "model supports. Returns the saved file paths; use readFile on a path to look at the result.",
            Handler = (
                    [Description("Detailed description of the image to generate.")] string prompt,
                    [Description("Where to save the image, e.g. assets/logo.png. Absolute or relative to the active project. With count > 1 a number is appended to the file name.")]
                    string path,
                    [Description("Image size from the model's 'sizes' in list_image_models, e.g. auto, 1024x1024, 1536x1024 (landscape) or 1024x1536 (portrait). Defaults to auto.")]
                    string? size = null,
                    [Description("Quality from the model's 'qualities' in list_image_models (e.g. auto, low, medium, high). Higher quality costs more. Ignored by models without quality levels. Defaults to auto.")]
                    string? quality = null,
                    [Description("Number of images to generate (1-4). Defaults to 1.")] int? count = null,
                    [Description("Image model id from list_image_models. Omit to use the default model.")] string? model = null,
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

    private static async Task<object> ListModelsAsync(IOneWareCloudAccess cloudAccess,
        CancellationToken cancellationToken)
    {
        var lookup = await GetModelsAsync(cloudAccess, cancellationToken);
        return new
        {
            signedIn = lookup.SignedIn,
            available = lookup.Error is null,
            reason = lookup.Error,
            defaultModel = lookup.Default?.Id,
            maxImagesPerCall = MaxImages,
            models = lookup.Models.Select(model => new
            {
                id = model.Id,
                displayName = model.DisplayName,
                description = model.Description,
                region = model.Region,
                sizes = model.Sizes,
                qualities = model.Qualities,
                maxImages = Math.Min(MaxImages, model.MaxImages),
                isDefault = model.IsDefault
            })
        };
    }

    private static async Task<object> GenerateAsync(IOneWareCloudAccess cloudAccess,
        IProjectExplorerService projectExplorerService, string prompt, string path, string? size, string? quality,
        int? count, string? model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return Error("A prompt is required.");

        var target = ResolvePath(projectExplorerService, path);
        if (target is null)
            return Error("Pass an absolute path, or open a project to save the image relative to it.");

        var lookup = await GetModelsAsync(cloudAccess, cancellationToken);
        if (lookup.Error is not null) return Error(lookup.Error);

        ImageModel? selected;
        if (string.IsNullOrWhiteSpace(model))
        {
            selected = lookup.Default;
        }
        else
        {
            selected = lookup.Models.FirstOrDefault(item =>
                string.Equals(item.Id, model.Trim(), StringComparison.OrdinalIgnoreCase));
            if (selected is null)
                return Error($"'{model}' is not an available image model. Use one of: " +
                             string.Join(", ", lookup.Models.Select(item => item.Id)) + ".");
        }

        if (selected is null) return Error("OneWare Cloud does not offer an image model for this organization yet.");

        var images = count ?? 1;
        var maxImages = Math.Min(MaxImages, selected.MaxImages);
        if (images < 1 || images > maxImages) return Error($"count must be between 1 and {maxImages}.");
        if (size is not null && !selected.Sizes.Contains(size, StringComparer.OrdinalIgnoreCase))
            return Error($"{selected.DisplayName} supports the sizes: {string.Join(", ", selected.Sizes)}.");
        if (quality is not null && selected.Qualities.Length > 0 &&
            !selected.Qualities.Contains(quality, StringComparer.OrdinalIgnoreCase))
            return Error($"{selected.DisplayName} supports the qualities: {string.Join(", ", selected.Qualities)}.");

        var body = new JsonObject
        {
            ["model"] = selected.Id,
            ["prompt"] = prompt,
            ["n"] = images
        };
        if (size is not null && !size.Equals("auto", StringComparison.OrdinalIgnoreCase))
            body["size"] = size.ToLowerInvariant();
        if (quality is not null && selected.Qualities.Length > 0 &&
            !quality.Equals("auto", StringComparison.OrdinalIgnoreCase))
            body["quality"] = quality.ToLowerInvariant();

        string token;
        try
        {
            token = await cloudAccess.GetAccessTokenAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Error(NotSignedInMessage);
        }

        try
        {
            using var response = await SendGenerationAsync(body, token, cloudAccess, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) _cache = null;
                return Error(await DescribeErrorAsync(response, cloudAccess, cancellationToken));
            }

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
                model = selected.Id,
                error = (string?)null
            };
        }
        catch (HttpRequestException)
        {
            return Error(UnreachableMessage);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Error("OneWare Cloud did not answer in time. Try again later or with fewer images.");
        }
    }

    private const string NotSignedInMessage =
        "The user is not signed in to OneWare Cloud. Ask them to sign in (OneWare Agents chat or the account " +
        "menu) and try again.";

    private const string UnreachableMessage =
        "OneWare Cloud could not be reached. Check the internet connection and try again.";

    /// <summary>
    /// Returns the image models of the signed-in user's organization, cached per user for a few minutes. Never
    /// throws for cloud or network problems; <see cref="ModelLookup.Error"/> explains why nothing is available.
    /// </summary>
    private static async Task<ModelLookup> GetModelsAsync(IOneWareCloudAccess cloudAccess,
        CancellationToken cancellationToken)
    {
        var userId = cloudAccess.UserId;
        if (string.IsNullOrWhiteSpace(userId)) return ModelLookup.Failed(false, NotSignedInMessage);

        await CacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache is { } cache && cache.UserId == userId && DateTime.UtcNow - cache.FetchedAt < ModelCacheDuration)
                return new ModelLookup(true, cache.Models, null);

            string token;
            try
            {
                token = await cloudAccess.GetAccessTokenAsync(cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return ModelLookup.Failed(false, "The OneWare Cloud session expired. Ask the user to sign in again.");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ModelListTimeout);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl(cloudAccess)}/images/models");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await HttpClient.SendAsync(request, timeout.Token);
                if (!response.IsSuccessStatusCode)
                    return ModelLookup.Failed(response.StatusCode != HttpStatusCode.Unauthorized,
                        await DescribeErrorAsync(response, cloudAccess, timeout.Token));

                var models = ParseModels(await response.Content.ReadAsStringAsync(timeout.Token));
                if (models.Length == 0)
                    return ModelLookup.Failed(true,
                        "OneWare Cloud does not offer an image model for this organization yet.");

                _cache = new ModelCache(userId, DateTime.UtcNow, models);
                return new ModelLookup(true, models, null);
            }
            catch (HttpRequestException)
            {
                return ModelLookup.Failed(true, UnreachableMessage);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ModelLookup.Failed(true, "OneWare Cloud did not answer in time. Try again later.");
            }
            catch (JsonException)
            {
                return ModelLookup.Failed(true, "OneWare Cloud returned an unexpected model list.");
            }
        }
        finally
        {
            CacheLock.Release();
        }
    }

    private static ImageModel[] ParseModels(string json)
    {
        var models = new List<ImageModel>();
        foreach (var item in JsonNode.Parse(json)?["data"] as JsonArray ?? [])
        {
            if (item?["id"]?.GetValue<string>() is not { Length: > 0 } id) continue;

            var sizes = ReadStrings(item["sizes"]);
            var qualities = ReadStrings(item["qualities"]);
            // Older servers do not advertise options; they accept the OpenAI images API values.
            var advertised = item["sizes"] is JsonArray;
            models.Add(new ImageModel(
                id,
                item["display_name"]?.GetValue<string>() ?? id,
                item["description"]?.GetValue<string>(),
                item["region"]?.GetValue<string>(),
                sizes.Length > 0 ? sizes : FallbackSizes,
                advertised ? qualities : FallbackQualities,
                item["max_images"] is JsonValue max && max.TryGetValue<int>(out var maxImages) && maxImages > 0
                    ? maxImages
                    : MaxImages,
                item["default"] is JsonValue isDefault && isDefault.TryGetValue<bool>(out var flag) && flag));
        }

        return models.ToArray();
    }

    private static string[] ReadStrings(JsonNode? node) =>
        node is JsonArray array
            ? array.Select(value => value is JsonValue text && text.TryGetValue<string>(out var s) ? s : null)
                .OfType<string>().ToArray()
            : [];

    /// <summary>
    ///     Posts the generation. A <c>cloud_ai_busy</c> answer means parallel requests of the organization collided
    ///     while reserving Credits and nothing was charged, so it is retried a few times.
    /// </summary>
    private static async Task<HttpResponseMessage> SendGenerationAsync(JsonObject body, string token,
        IOneWareCloudAccess cloudAccess, CancellationToken cancellationToken)
    {
        for (var attempt = 1;; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl(cloudAccess)}/images/generations")
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));

            var response = await HttpClient.SendAsync(request, cancellationToken);
            if (attempt >= MaxBusyAttempts || response.StatusCode != HttpStatusCode.ServiceUnavailable ||
                await ReadErrorCodeAsync(response, cancellationToken) != BusyErrorCode)
                return response;

            response.Dispose();
            await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(250, 750) * attempt), cancellationToken);
        }
    }

    private const int MaxBusyAttempts = 3;
    private const string BusyErrorCode = "cloud_ai_busy";

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await response.Content.LoadIntoBufferAsync(cancellationToken);
            return JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))?["error"]?["code"]
                ?.GetValue<string>();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string ApiBaseUrl(IOneWareCloudAccess cloudAccess) =>
        $"{cloudAccess.BaseUrl.TrimEnd('/')}/api/copilot/v1";

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
            (HttpStatusCode.NotFound, null) =>
                "This OneWare Cloud server does not support image generation yet.",
            _ when message is not null => message,
            _ when (int)response.StatusCode >= 500 =>
                $"OneWare Cloud failed to process the request ({(int)response.StatusCode} {response.ReasonPhrase}). " +
                "Retry it once; if several generate_image calls ran in parallel, run them one after another.",
            _ => $"OneWare Cloud rejected the request ({(int)response.StatusCode} {response.ReasonPhrase})."
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

    private sealed record ImageModel(
        string Id,
        string DisplayName,
        string? Description,
        string? Region,
        string[] Sizes,
        string[] Qualities,
        int MaxImages,
        bool IsDefault);

    private sealed record ModelCache(string UserId, DateTime FetchedAt, ImageModel[] Models);

    private sealed record ModelLookup(bool SignedIn, ImageModel[] Models, string? Error)
    {
        /// <summary>The model the server marks as default, else the first one.</summary>
        public ImageModel? Default => Models.FirstOrDefault(model => model.IsDefault) ?? Models.FirstOrDefault();

        public static ModelLookup Failed(bool signedIn, string error) => new(signedIn, [], error);
    }
}
