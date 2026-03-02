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
    private string? _codeVerifier;
    private string? _state;

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
        try
        {
            string? keycloakBaseUrl = await GetKeycloakAuthProviderUrlAsync();
            if (string.IsNullOrWhiteSpace(keycloakBaseUrl))
            {
                _logger.Error("Failed to get auth provider URL.");
                return (false, HttpStatusCode.ServiceUnavailable);
            }

            string tokenEndpoint = $"{keycloakBaseUrl}/protocol/openid-connect/token";
            
            RestRequest request = new RestRequest(tokenEndpoint, Method.Post);
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("grant_type", "refresh_token");
            request.AddParameter("client_id", "OneWareStudio");
            request.AddParameter("refresh_token", refreshToken);

            RestClient keycloakClient = new RestClient(_httpService.HttpClient, new RestClientOptions(keycloakBaseUrl));
            RestResponse response = await keycloakClient.ExecuteAsync(request);

            if (response.IsSuccessful && !string.IsNullOrWhiteSpace(response.Content))
            {
                JsonNode data = JsonSerializer.Deserialize<JsonNode>(response.Content)!;
                string? token = data["access_token"]?.GetValue<string>();
                string? newRefreshToken = data["refresh_token"]?.GetValue<string>();

                if (token == null || newRefreshToken == null)
                    throw new Exception("Token or refresh token not found");

                SaveCredentials(token, newRefreshToken);

                return (true, response.StatusCode);
            }

            return (false, response.StatusCode);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return (false, HttpStatusCode.InternalServerError);
        }
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

            RestClient restClient = new (_httpService.HttpClient);

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
        JwtSecurityToken? jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        string? userId = jwtToken.Claims.FirstOrDefault(x => x.Type == "sub")?.Value ?? null;
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

    private async Task<string?> GetKeycloakAuthProviderUrlAsync()
    {
        try
        {
            var request = new RestRequest("/api/auth/auth-provider");
            var response = await GetRestClient().ExecuteGetAsync(request);

            if (response.IsSuccessful && !string.IsNullOrWhiteSpace(response.Content))
            {
                return response.Content.Trim('"'); 
            }

            return null;
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return null;
        }
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier));
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    private static string GenerateState()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
    
    public async Task<bool> LoginWithBrowserAsync(CancellationToken cancellationToken = default)
    {
        var startNewListener = false;
        if (_port == null) startNewListener = true;
        _port ??= PlatformHelper.GetAvailablePort();
        var redirectUri = $"http://localhost:{_port}/callback";
        using HttpListener listener = new();

        var keycloakBaseUrl = await GetKeycloakAuthProviderUrlAsync();
        if (string.IsNullOrWhiteSpace(keycloakBaseUrl))
        {
            _logger.Error("Failed to get Keycloak auth provider URL");
            return false;
        }

        _codeVerifier = GenerateCodeVerifier();
        string codeChallenge = GenerateCodeChallenge(_codeVerifier);
        _state = GenerateState();
        
        string authUrl = $"{keycloakBaseUrl}/protocol/openid-connect/auth" +
                         $"?client_id=OneWareStudio" +
                         $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                         $"&response_type=code" +
                         $"&scope=openid profile email" +
                         $"&code_challenge={codeChallenge}" +
                         $"&code_challenge_method=S256" +
                         $"&state={_state}" +
                         $"&prompt=consent";

        if (startNewListener)
        {
            listener.Prefixes.Add($"http://localhost:{_port}/");
            listener.Start();
        }

        PlatformHelper.OpenHyperLink(authUrl);

        if (startNewListener)
            try
            {
                // Register cancellation callback to stop the listener
                using var registration = cancellationToken.Register(() => listener.Stop());

                var context = await listener.GetContextAsync();

                // Check if cancelled after getting context
                cancellationToken.ThrowIfCancellationRequested();

                var response = context.Response;

                await HandleKeycloakCallbackAsync(context, keycloakBaseUrl, redirectUri);
                
                // Redirect to Cloud index page
                var cloudHost = _settingService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareCloudHostKey)
                    .TrimEnd('/');
                response.Redirect($"{cloudHost}/");
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
                _codeVerifier = null;
                _state = null;
            }

        return startNewListener;
    }

    private async Task HandleKeycloakCallbackAsync(HttpListenerContext context, string keycloakBaseUrl, string redirectUri)
    {
        var query = HttpUtility.ParseQueryString(context!.Request!.Url!.Query);
        var code = query["code"];
        var state = query["state"];
        var error = query["error"];

        if (!string.IsNullOrWhiteSpace(error))
        {
            _logger.Error($"Authentication error: {error}");
            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            _logger.Error("No authorization code received from Keycloak");
            return;
        }

        if (state != _state)
        {
            _logger.Error("State mismatch in OAuth callback");
            return;
        }

        // Exchange authorization code for tokens
        await ExchangeCodeForTokensAsync(code, keycloakBaseUrl, redirectUri);
    }

    private async Task ExchangeCodeForTokensAsync(string code, string keycloakBaseUrl, string redirectUri)
    {
        try
        {
            var tokenEndpoint = $"{keycloakBaseUrl}/protocol/openid-connect/token";
            
            var request = new RestRequest(tokenEndpoint, Method.Post);
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("grant_type", "authorization_code");
            request.AddParameter("client_id", "OneWareStudio");
            request.AddParameter("code", code);
            request.AddParameter("redirect_uri", redirectUri);
            request.AddParameter("code_verifier", _codeVerifier);

            var keycloakClient = new RestClient(_httpService.HttpClient, new RestClientOptions(keycloakBaseUrl));
            var response = await keycloakClient.ExecuteAsync(request);

            if (response.IsSuccessful && !string.IsNullOrWhiteSpace(response.Content))
            {
                var tokenResponse = JsonSerializer.Deserialize<JsonNode>(response.Content)!;
                var accessToken = tokenResponse["access_token"]?.GetValue<string>();
                var refreshToken = tokenResponse["refresh_token"]?.GetValue<string>();

                if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
                {
                    _logger.Error("Access token or refresh token not found in Keycloak response");
                    return;
                }

                // Save the Keycloak tokens directly - backend expects the Keycloak signed JWT
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SaveCredentials(accessToken, refreshToken);
                    _settingService.Save(_paths.SettingsPath);
                });
            }
            else
            {
                _logger.Error($"Failed to exchange code for tokens: {response.StatusCode} - {response.Content}");
            }
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }


    private class LoginModel
    {
        [JsonPropertyName("email")] public required string Email { get; set; }

        [JsonPropertyName("password")] public required string Password { get; set; }
    }
}