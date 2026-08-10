using System.Text.Json;
using System.Text.Json.Nodes;
using GitCredentialManager;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using RestSharp;

namespace OneWare.SourceControl.LoginProviders;

public class GithubLoginProvider(ISettingsService settingsService, ILogger logger) : IDeviceCodeLoginProvider
{
    /// <summary>
    ///     Client id of the GitHub OAuth App used for the device authorization grant.
    ///     Can be overridden at runtime with the <c>ONEWARE_GITHUB_CLIENT_ID</c> environment variable.
    ///     The OAuth App must have "Device flow" enabled in its GitHub settings.
    /// </summary>
    public const string DefaultOAuthClientId = "Ov23li975pcVwwxkXu9L";

    private const string OAuthScopes = "repo gist read:org workflow read:user user:email";

    private const string DeviceCodeEndpoint = "https://github.com/login/device/code";
    private const string AccessTokenEndpoint = "https://github.com/login/oauth/access_token";

    private static readonly TimeSpan MinPollInterval = TimeSpan.FromSeconds(5);

    private static string ClientId =>
        Environment.GetEnvironmentVariable("ONEWARE_GITHUB_CLIENT_ID") is { Length: > 0 } fromEnv
            ? fromEnv
            : DefaultOAuthClientId;

    public bool IsDeviceCodeLoginAvailable => !string.IsNullOrWhiteSpace(ClientId);

    public string Name => "GitHub";

    public string Host => "https://github.com";

    public string GenerateLink =>
        "https://github.com/settings/tokens/new?description=OneWare%20Studio%20GitHub%20integration%20plugin&scopes=repo%2Cgist%2Cread%3Aorg%2Cworkflow%2Cread%3Auser%2Cuser%3Aemail";

    public async Task<bool> LoginAsync(string password)
    {
        try
        {
            var username = await GetUsernameAsync(password);

            if (username == null) return false;

            StoreCredentials(username, password);
            return true;
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
        }

        return false;
    }

    public async Task<DeviceCodeStartResult> StartDeviceCodeLoginAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsDeviceCodeLoginAvailable)
            return DeviceCodeStartResult.Error("No GitHub OAuth client id is configured.");

        try
        {
            var request = new RestRequest(DeviceCodeEndpoint);
            request.AddHeader("Accept", "application/json");
            request.AddParameter("client_id", ClientId);
            request.AddParameter("scope", OAuthScopes);

            var response = await new RestClient().ExecutePostAsync(request, cancellationToken);

            if (string.IsNullOrWhiteSpace(response.Content))
                return DeviceCodeStartResult.Error($"GitHub did not respond ({response.StatusCode}).");

            var data = JsonSerializer.Deserialize<JsonNode>(response.Content);

            if (data?["error"]?.GetValue<string>() is { } error)
                return DeviceCodeStartResult.Error(DescribeError(error,
                    data["error_description"]?.GetValue<string>()));

            var deviceCode = data?["device_code"]?.GetValue<string>();
            var userCode = data?["user_code"]?.GetValue<string>();
            var verificationUri = data?["verification_uri"]?.GetValue<string>() ?? "https://github.com/login/device";

            if (deviceCode == null || userCode == null)
                return DeviceCodeStartResult.Error("GitHub returned an unexpected device code response.");

            var interval = TimeSpan.FromSeconds(GetNumber(data?["interval"]) ?? 5);
            var expiresIn = TimeSpan.FromSeconds(GetNumber(data?["expires_in"]) ?? 900);

            return DeviceCodeStartResult.Ok(new DeviceCodeInfo(deviceCode, userCode, verificationUri,
                interval < MinPollInterval ? MinPollInterval : interval, DateTimeOffset.Now + expiresIn));
        }
        catch (OperationCanceledException)
        {
            return DeviceCodeStartResult.Error("Login cancelled.");
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
            return DeviceCodeStartResult.Error(e.Message);
        }
    }

    public async Task<DeviceCodeLoginResult> CompleteDeviceCodeLoginAsync(DeviceCodeInfo deviceCode,
        CancellationToken cancellationToken = default)
    {
        var interval = deviceCode.Interval;

        try
        {
            while (true)
            {
                await Task.Delay(interval, cancellationToken);

                if (DateTimeOffset.Now > deviceCode.ExpiresAt)
                    return DeviceCodeLoginResult.Failed(DeviceCodeLoginStatus.Expired,
                        "The code expired. Please try again.");

                var request = new RestRequest(AccessTokenEndpoint);
                request.AddHeader("Accept", "application/json");
                request.AddParameter("client_id", ClientId);
                request.AddParameter("device_code", deviceCode.DeviceCode);
                request.AddParameter("grant_type", "urn:ietf:params:oauth:grant-type:device_code");

                var response = await new RestClient().ExecutePostAsync(request, cancellationToken);

                if (string.IsNullOrWhiteSpace(response.Content)) continue;

                var data = JsonSerializer.Deserialize<JsonNode>(response.Content);

                if (data?["error"]?.GetValue<string>() is { } error)
                {
                    switch (error)
                    {
                        case "authorization_pending":
                            continue;
                        case "slow_down":
                            interval = TimeSpan.FromSeconds(GetNumber(data["interval"]) ?? interval.TotalSeconds + 5);
                            continue;
                        case "expired_token":
                            return DeviceCodeLoginResult.Failed(DeviceCodeLoginStatus.Expired,
                                "The code expired. Please try again.");
                        case "access_denied":
                            return DeviceCodeLoginResult.Failed(DeviceCodeLoginStatus.AccessDenied,
                                "Access was denied on GitHub.");
                        default:
                            return DeviceCodeLoginResult.Failed(DeviceCodeLoginStatus.Error,
                                DescribeError(error, data["error_description"]?.GetValue<string>()));
                    }
                }

                var accessToken = data?["access_token"]?.GetValue<string>();

                if (accessToken == null)
                    return DeviceCodeLoginResult.Failed(DeviceCodeLoginStatus.Error,
                        "GitHub returned an unexpected token response.");

                var username = await GetUsernameAsync(accessToken, cancellationToken);

                if (username == null)
                    return DeviceCodeLoginResult.Failed(DeviceCodeLoginStatus.Error,
                        "Could not read the GitHub account for the received token.");

                StoreCredentials(username, accessToken);

                return DeviceCodeLoginResult.Ok(username);
            }
        }
        catch (OperationCanceledException)
        {
            return DeviceCodeLoginResult.Failed(DeviceCodeLoginStatus.Cancelled, "Login cancelled.");
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
            return DeviceCodeLoginResult.Failed(DeviceCodeLoginStatus.Error, e.Message);
        }
    }

    private static async Task<string?> GetUsernameAsync(string token, CancellationToken cancellationToken = default)
    {
        var client = new RestClient("https://api.github.com");
        var request = new RestRequest("/user");
        request.AddHeader("Authorization", $"Bearer {token}");
        request.AddHeader("Accept", "application/vnd.github+json");

        var response = await client.ExecuteGetAsync(request, cancellationToken);

        if (string.IsNullOrWhiteSpace(response.Content)) return null;

        var data = JsonSerializer.Deserialize<JsonNode>(response.Content);

        return data?["login"]?.GetValue<string>();
    }

    private void StoreCredentials(string username, string token)
    {
        var store = CredentialManager.Create("oneware");
        store.AddOrUpdate(Host, username, token);

        settingsService.SetSettingValue(SourceControlModule.GitHubAccountNameKey, username);
        settingsService.Save(ContainerLocator.Container.Resolve<IPaths>().SettingsPath);
    }

    private static double? GetNumber(JsonNode? node)
    {
        try
        {
            return node?.GetValue<double>();
        }
        catch
        {
            return double.TryParse(node?.ToString(), out var parsed) ? parsed : null;
        }
    }

    private static string DescribeError(string error, string? description)
    {
        return error switch
        {
            "device_flow_disabled" =>
                "Device flow is not enabled for the configured GitHub OAuth App.",
            "unauthorized_client" =>
                "The configured GitHub OAuth client id is not allowed to use the device flow.",
            _ => description ?? error
        };
    }
}
