using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private readonly Dictionary<string, JwtSecurityToken> _jwtBearerTokenCache = new();

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

    /// <summary>
    ///     Refreshes the refresh token if it expires within 14 days
    ///     Should be called on application startup
    /// </summary>
    public async Task RefreshRefreshTokenAsync()
    {
        try
        {
            var userId = _settingService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareAccountUserIdKey);
            if (string.IsNullOrWhiteSpace(userId))
                return;

            string? refreshToken = await GetRefreshToken(userId);

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                _logger.Warning("No refresh token found for configured cloud account. Logging out local session.");
                Logout(userId);
                return;
            }

            DateTime? expiryDate = TryGetRefreshTokenExpiry(refreshToken);

            if (expiryDate.HasValue)
            {
                if (expiryDate.Value <= DateTime.UtcNow)
                {
                    _logger.Warning("Stored refresh token has expired. Logging out local session.");
                    Logout(userId);
                    return;
                }

                var daysUntilExpiry = (expiryDate.Value - DateTime.UtcNow).TotalDays;

                if (daysUntilExpiry <= 14)
                {
                    // Renew the refresh token (this also updates the bearer token as a side effect)
                    var (success, status) = await RenewRefreshTokenAsync(refreshToken);

                    if (success)
                    {
                        _logger.Log("Refresh token successfully renewed on startup.");
                    }
                    else
                    {
                        _logger.Warning($"Failed to renew refresh token on startup. Status: {status}");

                        if (ShouldLogoutAfterTokenRefreshFailure(status))
                            Logout(userId);
                    }
                }
            }
        }
        catch (Exception e)
        {
            _logger.Error($"Error during startup token check: {e.Message}", e);
        }
    }

    /// <summary>
    ///     Tries to decode the refresh token and extract its expiry date
    /// </summary>
    private DateTime? TryGetRefreshTokenExpiry(string refreshToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            
            // Check if it's a valid JWT
            if (!handler.CanReadToken(refreshToken))
                return null;
            
            var jwtToken = handler.ReadJwtToken(refreshToken);
            
            // Return the expiry date
            return jwtToken.ValidTo;
        }
        catch (Exception e)
        {
            _logger.Warning($"Could not decode refresh token as JWT: {e.Message}");
            return null;
        }
    }

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

            _jwtBearerTokenCache.TryGetValue(userId, out var existingToken);

            if (existingToken?.ValidTo > DateTime.UtcNow.AddMinutes(2))
                return (existingToken, HttpStatusCode.NoContent);
            
            await RefreshRefreshTokenAsync();
            
            var (result, status) = await RefreshFromUserIdAsync(userId);

            if (!result)
            {
                if (ShouldLogoutAfterTokenRefreshFailure(status))
                    Logout(userId);

                return (null, status);
            }

            if (!_jwtBearerTokenCache.TryGetValue(userId, out var regeneratedToken)) return (null, status);

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
            string? refreshToken = await GetRefreshToken(userId);

            if (refreshToken == null)
                return (false, HttpStatusCode.Unauthorized);

            var result = await RefreshBearerTokenAsync(refreshToken);

            return result;
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return (false, HttpStatusCode.NoContent);
        }
    }

    private async Task<(bool success, HttpStatusCode status)> RefreshBearerTokenAsync(string refreshToken)
    {
        try
        {
            string? authBaseUrl = await GetAuthProviderUrlAsync();
            if (string.IsNullOrWhiteSpace(authBaseUrl))
            {
                _logger.Error("Failed to get auth provider URL.");
                return (false, HttpStatusCode.ServiceUnavailable);
            }

            string tokenEndpoint = $"{authBaseUrl}/protocol/openid-connect/token";
            
            RestRequest request = new RestRequest(tokenEndpoint, Method.Post);
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("grant_type", "refresh_token");
            request.AddParameter("client_id", "OneWareStudio");
            request.AddParameter("refresh_token", refreshToken);

            RestClient authClient = new RestClient(_httpService.HttpClient, new RestClientOptions(authBaseUrl));
            RestResponse response = await authClient.ExecuteAsync(request);

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

    /// <summary>
    ///     Renews the refresh token proactively when it's close to expiration (e.g., within 14 days).
    ///     This uses the same Keycloak endpoint as RefreshBearerTokenAsync, but is called explicitly
    ///     to extend the refresh token's validity period.
    ///     Note: Keycloak returns both a new access token and a new refresh token.
    /// </summary>
    private async Task<(bool success, HttpStatusCode status)> RenewRefreshTokenAsync(string refreshToken)
    {
        try
        {
            string? authBaseUrl = await GetAuthProviderUrlAsync();
            if (string.IsNullOrWhiteSpace(authBaseUrl))
            {
                _logger.Error("Failed to get auth provider URL.");
                return (false, HttpStatusCode.ServiceUnavailable);
            }

            string tokenEndpoint = $"{authBaseUrl}/protocol/openid-connect/token";
            
            RestRequest request = new RestRequest(tokenEndpoint, Method.Post);
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("grant_type", "refresh_token");
            request.AddParameter("client_id", "OneWareStudio");
            request.AddParameter("refresh_token", refreshToken);

            RestClient authClient = new RestClient(_httpService.HttpClient, new RestClientOptions(authBaseUrl));
            RestResponse response = await authClient.ExecuteAsync(request);

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

            _jwtBearerTokenCache.Remove(userId);
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

        _jwtBearerTokenCache[userId] = jwtToken;

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

    private async Task<string?> GetAuthProviderUrlAsync()
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
    
    public async Task<bool> LoginAsync(CancellationToken cancellationToken = default)
    {
        var startNewListener = false;
        if (_port == null) startNewListener = true;
        _port ??= PlatformHelper.GetAvailablePort();
        var redirectUri = $"http://localhost:{_port}/callback";
        using HttpListener listener = new();

        var authProviderBaseUrl = await GetAuthProviderUrlAsync();
        if (string.IsNullOrWhiteSpace(authProviderBaseUrl))
        {
            _logger.Error("Failed to get auth provider URL");
            return false;
        }
        
        _codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(_codeVerifier);
        _state = GenerateState();

        var authQueryParams = HttpUtility.ParseQueryString(string.Empty);
        authQueryParams["client_id"] = "OneWareStudio";
        authQueryParams["redirect_uri"] = redirectUri;
        authQueryParams["response_type"] = "code";
        authQueryParams["scope"] = "openid profile email offline_access";
        authQueryParams["code_challenge"] = codeChallenge;
        authQueryParams["code_challenge_method"] = "S256";
        authQueryParams["state"] = _state;
        var authUrl = $"{authProviderBaseUrl}/protocol/openid-connect/auth?{authQueryParams}";

        if (startNewListener)
        {
            listener.Prefixes.Add($"http://localhost:{_port}/");
            listener.Start();
        }

        PlatformHelper.OpenHyperLink(authUrl);

        if (startNewListener)
            try
            {
                using var registration = cancellationToken.Register(() => listener.Stop());

                var context = await listener.GetContextAsync();
                cancellationToken.ThrowIfCancellationRequested();

                var response = context.Response;

                var query = HttpUtility.ParseQueryString(context.Request!.Url!.Query);
                var code = query["code"];
                var state = query["state"];
                var error = query["error"];
                
                if (!string.IsNullOrWhiteSpace(error))
                {
                    _logger.Error($"Authentication error: {error}");
                    response.StatusCode = 400;
                    response.Close();
                    return false;
                }

                if (string.IsNullOrWhiteSpace(code) || state != _state)
                {
                    _logger.Error("Invalid callback (missing code or state mismatch)");
                    response.StatusCode = 400;
                    response.Close();
                    return false;
                }

                await ExchangeCodeForTokensAsync(code, authProviderBaseUrl, redirectUri);

                var cloudHost = _settingService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareCloudHostKey)
                    .TrimEnd('/');
                response.Redirect($"{cloudHost}/");
                response.KeepAlive = false;
                response.Close();
                
                return true;
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                // Listener was stopped due to cancellation, ignore
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
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

    private async Task ExchangeCodeForTokensAsync(string code, string authProviderBaseUrl, string redirectUri)
    {
        try
        {
            var tokenEndpoint = $"{authProviderBaseUrl}/protocol/openid-connect/token";

            var request = new RestRequest(tokenEndpoint, Method.Post);
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("grant_type", "authorization_code");
            request.AddParameter("client_id", "OneWareStudio");
            request.AddParameter("code", code);
            request.AddParameter("redirect_uri", redirectUri);
            request.AddParameter("code_verifier", _codeVerifier);

            var authClient = new RestClient(_httpService.HttpClient, new RestClientOptions(authProviderBaseUrl));
            var response = await authClient.ExecuteAsync(request);

            if (response.IsSuccessful && !string.IsNullOrWhiteSpace(response.Content))
            {
                var tokenResponse = JsonSerializer.Deserialize<JsonNode>(response.Content)!;
                var accessToken = tokenResponse["access_token"]?.GetValue<string>();
                var refreshToken = tokenResponse["refresh_token"]?.GetValue<string>();

                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    _logger.Error("Access token not found in response");
                    return;
                }

                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    _logger.Error("Refresh token not found in response");
                    return;
                }

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

    private async Task<string?> GetRefreshToken(string userId)
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

        return refreshToken;
    }

    private static bool ShouldLogoutAfterTokenRefreshFailure(HttpStatusCode status)
    {
        return status is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
    }
}
