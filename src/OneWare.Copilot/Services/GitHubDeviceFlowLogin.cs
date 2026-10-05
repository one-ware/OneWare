using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.ViewModels;

namespace OneWare.Copilot.Services;

/// <summary>
/// GitHub OAuth device authorization grant (RFC 8628) for the bundled Copilot runtime, which has no
/// <c>copilot login</c> command. The resulting OAuth user token is handed to the runtime, see
/// <see href="https://docs.github.com/en/copilot/how-tos/copilot-sdk/setup/github-oauth" />.
/// </summary>
internal static class GitHubDeviceFlowLogin
{
    /// <summary>
    /// Client id of the OneWare GitHub OAuth App (shared with the source control GitHub login).
    /// Can be overridden with the <c>ONEWARE_GITHUB_CLIENT_ID</c> environment variable.
    /// The OAuth App must have "Device flow" enabled.
    /// </summary>
    private const string DefaultOAuthClientId = "Ov23li975pcVwwxkXu9L";

    /// <summary>Same scopes the Copilot CLI requests, so its built-in GitHub tools keep working.</summary>
    private const string OAuthScopes = "read:user read:org repo gist";

    public const string GitHubHost = "https://github.com";

    private const string DeviceCodeEndpoint = "https://github.com/login/device/code";
    private const string AccessTokenEndpoint = "https://github.com/login/oauth/access_token";

    private static readonly TimeSpan MinPollInterval = TimeSpan.FromSeconds(5);

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private static string ClientId =>
        Environment.GetEnvironmentVariable("ONEWARE_GITHUB_CLIENT_ID") is { Length: > 0 } fromEnv
            ? fromEnv
            : DefaultOAuthClientId;

    /// <summary>
    /// Runs the device flow and returns the OAuth access token, or <c>null</c> when the login failed.
    /// Progress and errors are reported through <paramref name="prompt" />.
    /// </summary>
    public static async Task<string?> RequestTokenAsync(IDeviceCodeLoginPrompt prompt,
        CancellationToken cancellationToken)
    {
        prompt.Status = "Requesting device code...";

        using var start = await PostAsync(DeviceCodeEndpoint, new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["scope"] = OAuthScopes
        }, cancellationToken);

        if (start == null)
        {
            prompt.Status = "GitHub did not respond. Check your internet connection and try again.";
            return null;
        }

        var root = start.RootElement;

        if (GetString(root, "error") is { } startError)
        {
            prompt.Status = DescribeError(startError, GetString(root, "error_description"));
            return null;
        }

        var deviceCode = GetString(root, "device_code");
        var userCode = GetString(root, "user_code");
        var verificationUri = GetString(root, "verification_uri") ?? "https://github.com/login/device";

        if (deviceCode == null || userCode == null)
        {
            prompt.Status = "GitHub returned an unexpected device code response.";
            return null;
        }

        var interval = TimeSpan.FromSeconds(GetNumber(root, "interval") ?? 5);
        if (interval < MinPollInterval) interval = MinPollInterval;
        var expiresAt = DateTimeOffset.Now + TimeSpan.FromSeconds(GetNumber(root, "expires_in") ?? 900);

        prompt.UserCode = userCode;
        prompt.VerificationUrl = verificationUri;
        prompt.Status = "Waiting for authorization in the browser...";

        PlatformHelper.OpenHyperLink(verificationUri);

        while (true)
        {
            await Task.Delay(interval, cancellationToken);

            if (DateTimeOffset.Now > expiresAt)
            {
                prompt.Status = "The code expired. Please try again.";
                return null;
            }

            using var poll = await PostAsync(AccessTokenEndpoint, new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["device_code"] = deviceCode,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
            }, cancellationToken);

            if (poll == null) continue;

            var pollRoot = poll.RootElement;

            switch (GetString(pollRoot, "error"))
            {
                case null:
                    break;
                case "authorization_pending":
                    continue;
                case "slow_down":
                    interval = TimeSpan.FromSeconds(GetNumber(pollRoot, "interval") ?? interval.TotalSeconds + 5);
                    continue;
                case "expired_token":
                    prompt.Status = "The code expired. Please try again.";
                    return null;
                case "access_denied":
                    prompt.Status = "Access was denied on GitHub.";
                    return null;
                case var error:
                    prompt.Status = DescribeError(error, GetString(pollRoot, "error_description"));
                    return null;
            }

            if (GetString(pollRoot, "access_token") is { Length: > 0 } accessToken) return accessToken;

            prompt.Status = "GitHub returned an unexpected token response.";
            return null;
        }
    }

    private static async Task<JsonDocument?> PostAsync(string url, Dictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new FormUrlEncodedContent(form);

            using var response = await Http.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            return string.IsNullOrWhiteSpace(content) ? null : JsonDocument.Parse(content);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException ||
                                  (e is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Transient network problems: the caller reports or retries.
            return null;
        }
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? GetNumber(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number) return value.GetDouble();
        return value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;
    }

    private static string DescribeError(string error, string? description) => error switch
    {
        "device_flow_disabled" => "Device flow is not enabled for the OneWare GitHub OAuth App.",
        "unauthorized_client" => "The OneWare GitHub OAuth App is not allowed to use the device flow.",
        _ => description ?? error
    };
}
