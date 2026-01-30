using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Web;
using Avalonia.Threading;
using GitCredentialManager;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using RestSharp;

namespace OneWare.CloudIntegration.Services;

public sealed class OneWareCloudLoginService
{
    private readonly IHttpService _httpService;
    private readonly Dictionary<string, JwtSecurityToken> _jwtTokenCache = new();

    private readonly ILogger _logger;
    private readonly IPaths _paths;
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
    private readonly ISettingsService _settingService;
    private readonly string _tokenPath;

    private int? _port;

    public OneWareCloudLoginService(ILogger logger, ISettingsService settingService, IHttpService httpService,
        IPaths paths)
    {
        _logger = logger;
        _settingService = settingService;
        _httpService = httpService;
        _paths = paths;
        _tokenPath = Path.Combine(paths.AppDataDirectory, "Cloud");

        settingService.GetSettingObservable<string>(OneWareCloudIntegrationModule.OneWareCloudHostKey)
            .Skip(1)
            .Subscribe(x =>
            {
                Logout(settingService.GetSettingValue<string>(OneWareCloudIntegrationModule
                    .OneWareAccountUserIdKey));
            });

        OneWareCloudIsUsed =
            _settingService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareCloudHostKey) ==
            OneWareCloudIntegrationModule.CredentialStore;
    }

    public bool OneWareCloudIsUsed { get; }

    public RestClient GetRestClient()
    {
        var baseUrl = _settingService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareCloudHostKey);
        return new RestClient(_httpService.HttpClient, new RestClientOptions(baseUrl));
    }

    public Task<(JwtSecurityToken? token, HttpStatusCode status)> GetLoggedInJwtTokenAsync()
    {
        var userId = _settingService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareAccountUserIdKey);

        return GetJwtTokenAsync(userId);
    }

    /// <summary>
    ///     Returns a JWT Token that has at least 2 minutes left before expiration
    ///     If the token is null, the HttpStatusCode can be used to get the reason
    /// </summary>
    public async Task<(JwtSecurityToken? token, HttpStatusCode status)> GetJwtTokenAsync(string userId)
    {
        await _semaphoreSlim.WaitAsync();

        try
        {
            if (string.IsNullOrWhiteSpace(userId)) return (null, HttpStatusCode.Unauthorized);

            _jwtTokenCache.TryGetValue(userId, out var existingToken);

            if (existingToken?.ValidTo > DateTime.UtcNow.AddMinutes(2))
                return (existingToken, HttpStatusCode.NoContent);

            var (result, status) = await RefreshFromUserIdAsync(userId);

            if (!result) return (null, status);

            if (!_jwtTokenCache.TryGetValue(userId, out var regeneratedToken)) return (null, status);

            return (regeneratedToken, status);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public async Task<(bool success, HttpStatusCode status)> RefreshFromUserIdAsync(string userId)
    {
        try
        {
            string? refreshToken = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var tokenPath = Path.Combine(_tokenPath, $"{userId}.bin");
                if (File.Exists(tokenPath))
                {
                    var encrypted = await File.ReadAllBytesAsync(tokenPath);
                    var plaintext = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                    refreshToken = Encoding.UTF8.GetString(plaintext);
                }
            }
            else
            {
                var store = CredentialManager.Create("oneware");

                var cred = store.Get(OneWareCloudIntegrationModule.CredentialStore, userId);
                refreshToken = cred?.Password;
            }

            if (refreshToken == null)
                return (false, HttpStatusCode.Unauthorized);

            var result = await RefreshAsync(refreshToken);

            return result;
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return (false, HttpStatusCode.NoContent);
        }
    }

    private async Task<(bool success, HttpStatusCode status)> RefreshAsync(string refreshToken)
    {
        var request = new RestRequest("/api/auth/refresh");
        request.AddJsonBody(new RefreshModel
        {
            RefreshToken = refreshToken
        });
        var response = await GetRestClient().ExecutePostAsync(request);

        if (response.IsSuccessful)
        {
            var data = JsonSerializer.Deserialize<JsonNode>(response.Content!)!;
            var token = data["token"]?.GetValue<string>();
            var newRefreshToken = data["refreshToken"]?.GetValue<string>();

            if (token == null || newRefreshToken == null)
                throw new Exception("Token or refresh token not found");

            SaveCredentials(token, newRefreshToken);

            return (true, response.StatusCode);
        }

        return (false, response.StatusCode);
    }

    public async Task<(bool success, HttpStatusCode status)> LoginAsync(string email, string password)
    {
        try
        {
            var request = new RestRequest("/api/auth/login");
            request.AddJsonBody(new LoginModel
            {
                Email = email,
                Password = password
            });

            var response = await GetRestClient().ExecutePostAsync(request);

            if (response.IsSuccessful)
            {
                var data = JsonSerializer.Deserialize<JsonNode>(response.Content!)!;

                var token = data["token"]?.GetValue<string>();
                var refreshToken = data["refreshToken"]?.GetValue<string>();

                if (token == null || refreshToken == null) throw new Exception("Token or refresh token not found");

                SaveCredentials(token, refreshToken);

                _settingService.Save(_paths.SettingsPath);

                return (true, HttpStatusCode.OK);
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests) return (false, response.StatusCode);

            return (false, response.StatusCode);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }

        return (false, HttpStatusCode.NoContent);
    }

    public void Logout(string userId)
    {
        _settingService.SetSettingValue(OneWareCloudIntegrationModule.OneWareAccountUserIdKey, "");
        _ = ContainerLocator.Container.Resolve<OneWareCloudNotificationService>().DisconnectAsync();

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var tokenPath = Path.Combine(_tokenPath, $"{userId}.bin");
                if (File.Exists(tokenPath))
                    File.Delete(tokenPath);
            }
            else
            {
                var store = CredentialManager.Create("oneware");
                store.Remove(OneWareCloudIntegrationModule.CredentialStore, userId);
            }

            _jwtTokenCache.Remove(userId);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }

    public async Task<bool> SendFeedbackAsync(string category, string message, string? mail = null)
    {
        try
        {
            RestRequest? request;
            JwtSecurityToken? jwt = null;
            if (OneWareCloudIsUsed) (jwt, _) = await GetLoggedInJwtTokenAsync();

            if (jwt == null)
            {
                request = new RestRequest("/api/feedback/anonymous");
            }
            else
            {
                request = new RestRequest("/api/feedback");
                request.AddHeader("Authorization", $"Bearer {jwt.RawData}");
            }

            request.AddHeader("Accept", "application/json");
            request.AddJsonBody(new
            {
                Category = category,
                Message = message,
                Email = mail
            });

            var restClient =
                new RestClient(_httpService.HttpClient, new RestClientOptions("https://cloud.one-ware.com"));

            var response = await restClient.ExecutePostAsync(request);
            return response.IsSuccessful;
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }

        return false;
    }

    private void SaveCredentials(string jwt, string refreshToken)
    {
        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        var userId = jwtToken.Claims.FirstOrDefault(x => x.Type == "nameid")?.Value ?? null;
        if (userId == null) throw new Exception("User ID not found in token");

        _jwtTokenCache[userId] = jwtToken;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Directory.CreateDirectory(_tokenPath);
                var tokenPath = Path.Combine(_tokenPath, $"{userId}.bin");

                var plaintext = Encoding.UTF8.GetBytes(refreshToken);
                var encrypted = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(tokenPath, encrypted);
            }
            else
            {
                var store = CredentialManager.Create("oneware");
                store.AddOrUpdate(OneWareCloudIntegrationModule.CredentialStore, userId, refreshToken);
            }
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }

        _settingService.SetSettingValue(OneWareCloudIntegrationModule.OneWareAccountUserIdKey, userId);
        _settingService.Save(_paths.SettingsPath);
    }


    //Returns if new listener was started
    public async Task<bool> LoginWithBrowserAsync(CancellationToken cancellationToken = default)
    {
        var startNewListener = false;
        if (_port == null) startNewListener = true;
        _port ??= PlatformHelper.GetAvailablePort();
        var prefix = $"http://{IPAddress.Loopback}:{_port}/";
        var host = _settingService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareCloudHostKey)
            .TrimEnd('/');
        var url = $"{host}/Account/LoginInApplication/?redirectPort={_port}";
        using HttpListener listener = new();

        if (startNewListener)
        {
            listener.Prefixes.Add(prefix);
            listener.Start();
        }

        PlatformHelper.OpenHyperLink(url);

        if (startNewListener)
            try
            {
                // Register cancellation callback to stop the listener
                using var registration = cancellationToken.Register(() => listener.Stop());

                var context = await listener.GetContextAsync();

                // Check if cancelled after getting context
                cancellationToken.ThrowIfCancellationRequested();

                var response = context.Response;

                await HandleLoginResponseAsync(context);
                response.Redirect(host);
                response.KeepAlive = false;
                response.Close();
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                // Listener was stopped due to cancellation, ignore
            }
            finally
            {
                listener.Stop();
                _port = null;
            }

        return startNewListener;
    }

    private async Task HandleLoginResponseAsync(HttpListenerContext context)
    {
        var query = HttpUtility.ParseQueryString(context!.Request!.Url!.Query);
        var refreshToken = query["refreshToken"];

        if (refreshToken == null) return;

        await Dispatcher.UIThread.InvokeAsync(() => RefreshAsync(refreshToken));
        _settingService.Save(_paths.SettingsPath);
    }

    private class LoginModel
    {
        [JsonPropertyName("email")] public required string Email { get; set; }

        [JsonPropertyName("password")] public required string Password { get; set; }
    }

    private class RefreshModel
    {
        [JsonPropertyName("refreshToken")] public required string RefreshToken { get; set; }
    }
}