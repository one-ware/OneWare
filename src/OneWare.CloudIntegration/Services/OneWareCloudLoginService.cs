using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
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
using OneWare.CloudIntegration.Dto;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using RestSharp;

namespace OneWare.CloudIntegration.Services;

public sealed class OneWareCloudLoginService : IOneWareCloudAccess
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

    private string? _offlineCodeVerifier;
    private string? _offlineState;

    private string? _authProviderCacheHost;
    private string? _authProviderCacheUrl;

    private CancellationTokenSource? _pendingLoginCts;

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
                CancelPendingLogin();

                // Log out before clearing the cache, so the token is revoked at the previous host's auth provider
                Logout(settingService.GetSettingValue<string>(OneWareCloudIntegrationModule
                    .OneWareAccountUserIdKey));

                _authProviderCacheHost = null;
                _authProviderCacheUrl = null;
            });
    }

    public bool OfficialCloudIsUsed => _settingService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareCloudHostKey).EqualUrls(OneWareCloudIntegrationModule.OfficialHost);

    public string BaseUrl =>
        _settingService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareCloudHostKey).TrimEnd('/');

    public string? UserId => NormalizeUserId(
        _settingService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareAccountUserIdKey));

    // Deferred because the user id setting is registered after this service may have been created.
    public IObservable<string?> UserIdObservable => Observable.Defer(() =>
            _settingService.GetSettingObservable<object?>(OneWareCloudIntegrationModule.OneWareAccountUserIdKey)
                .Select(value => NormalizeUserId(value?.ToString())))
        .DistinctUntilChanged();

    private static string? NormalizeUserId(string? userId) => string.IsNullOrWhiteSpace(userId) ? null : userId;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (token, status) = await GetLoggedInJwtTokenAsync();
        cancellationToken.ThrowIfCancellationRequested();
        if (token is null || string.IsNullOrWhiteSpace(token.RawData))
            throw new InvalidOperationException($"OneWare Cloud authentication failed ({(int)status} {status}).");

        return token.RawData;
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

            var result = await RefreshTokensAsync(refreshToken);

            return result;
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return (false, HttpStatusCode.NoContent);
        }
    }

    private async Task<(bool success, HttpStatusCode status)> RefreshTokensAsync(string refreshToken)
    {
        try
        {
            string? authBaseUrl = await GetAuthProviderUrlAsync();
            if (string.IsNullOrWhiteSpace(authBaseUrl))
                return (false, HttpStatusCode.ServiceUnavailable);

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

                await SaveCredentialsAsync(token, newRefreshToken);

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

        if (string.IsNullOrWhiteSpace(userId)) return;

        try
        {
            string? refreshToken = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var tokenPath = Path.Combine(_tokenPath, $"{userId}.bin");
                if (File.Exists(tokenPath))
                {
                    refreshToken = TryReadWindowsRefreshToken(tokenPath);
                    File.Delete(tokenPath);
                }
            }
            else
            {
                var store = CredentialManager.Create("oneware");
                refreshToken = store.Get(OneWareCloudIntegrationModule.CredentialStore, userId)?.Password;
                store.Remove(OneWareCloudIntegrationModule.CredentialStore, userId);
            }

            _jwtBearerTokenCache.Remove(userId);

            if (!string.IsNullOrWhiteSpace(refreshToken) && _authProviderCacheUrl is { } authProviderUrl)
                _ = RevokeRefreshTokenAsync(authProviderUrl, refreshToken);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }

    private string? TryReadWindowsRefreshToken(string tokenPath)
    {
        try
        {
            var plaintext = ProtectedData.Unprotect(File.ReadAllBytes(tokenPath), null,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (Exception e)
        {
            _logger.Warning($"Could not read the stored refresh token: {e.Message}", null, false);
            return null;
        }
    }

    /// <summary>Ends the offline session at the auth provider, so the signed-out token can no longer be used.</summary>
    private async Task RevokeRefreshTokenAsync(string authProviderBaseUrl, string refreshToken)
    {
        try
        {
            var request = new RestRequest($"{authProviderBaseUrl}/protocol/openid-connect/revoke", Method.Post);
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("client_id", "OneWareStudio");
            request.AddParameter("token", refreshToken);
            request.AddParameter("token_type_hint", "refresh_token");

            var authClient = new RestClient(_httpService.HttpClient, new RestClientOptions(authProviderBaseUrl));
            var response = await authClient.ExecuteAsync(request);
            if (!response.IsSuccessful)
                _logger.Warning($"Revoking the OneWare Cloud session failed: {(int)response.StatusCode}", null, false);
        }
        catch (Exception e)
        {
            _logger.Warning($"Revoking the OneWare Cloud session failed: {e.Message}", null, false);
        }
    }

    public async Task<bool> SendFeedbackAsync(string category, string message, string? mail = null)
    {
        try
        {
            RestRequest? request;
            
            if (OfficialCloudIsUsed && await GetLoggedInJwtTokenAsync() is ({ } token, _))
            {
                request = new RestRequest("/api/feedback");
                request.AddHeader("Authorization", $"Bearer {token.RawData}");
            }
            else
            {
                request = new RestRequest("/api/feedback/anonymous");
            }

            request.AddHeader("Accept", "application/json");
            request.AddJsonBody(new
            {
                Category = category,
                Message = message,
                Email = mail
            });

            RestClient restClient = new(_httpService.HttpClient, new RestClientOptions(OneWareCloudIntegrationModule.OfficialHost));

            var response = await restClient.ExecutePostAsync(request);
            return response.IsSuccessful;
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }

        return false;
    }

    private async Task SaveCredentialsAsync(string jwt, string refreshToken)
    {
        JwtSecurityToken? jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        string? userId = jwtToken.Claims.FirstOrDefault(x => x.Type == "sub")?.Value ?? null;
        if (userId == null) throw new Exception("User ID not found in token");

        _jwtBearerTokenCache[userId] = jwtToken;

        // The platform credential store can be slow (keyring/dbus, DPAPI), keep it off the UI thread
        await Task.Run(() => StoreRefreshToken(userId, refreshToken));

        _settingService.SetSettingValue(OneWareCloudIntegrationModule.OneWareAccountUserIdKey, userId);
        _settingService.Save(_paths.SettingsPath);
    }

    private void StoreRefreshToken(string userId, string refreshToken)
    {
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
    }

    /// <summary>
    ///     Cancels a browser login that is still waiting for the callback, so a new login can be started.
    /// </summary>
    public void CancelPendingLogin()
    {
        var cts = _pendingLoginCts;
        _pendingLoginCts = null;
        _port = null;
        _codeVerifier = null;
        _state = null;
        _offlineCodeVerifier = null;
        _offlineState = null;

        if (cts == null) return;

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Login already finished
        }
    }

    private async Task<string?> GetAuthProviderUrlAsync()
    {
        var host = _settingService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareCloudHostKey);

        if (_authProviderCacheUrl != null && _authProviderCacheHost != null && _authProviderCacheHost.EqualUrls(host))
            return _authProviderCacheUrl;

        try
        {
            var request = new RestRequest("/api/auth/auth-provider");
            var response = await GetRestClient().ExecuteGetAsync(request);

            if (response.IsSuccessful && !string.IsNullOrWhiteSpace(response.Content))
            {
                _authProviderCacheHost = host;
                _authProviderCacheUrl = response.Content.Trim('"');
                return _authProviderCacheUrl;
            }

            _logger.Warning("Failed to get auth provider URL.", null, false);

            return null;
        }
        catch (Exception e)
        {
            if (!IsConnectivityFailure(e))
                _logger.Error(e.Message, e, showOutput: false);

            return null;
        }
    }

    private static bool IsConnectivityFailure(Exception exception)
    {
        return exception switch
        {
            HttpRequestException => true,
            SocketException => true,
            TimeoutException => true,
            TaskCanceledException => true,
            OperationCanceledException => true,
            _ when exception.InnerException is not null => IsConnectivityFailure(exception.InnerException),
            _ => false
        };
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
        var startNewListener = _port == null;
        _port ??= PlatformHelper.GetAvailablePort();
        var redirectUri = $"http://localhost:{_port}/callback";
        using HttpListener listener = new();

        var authProviderBaseUrl = await GetAuthProviderUrlAsync();
        if (string.IsNullOrWhiteSpace(authProviderBaseUrl))
        {
            if (startNewListener) _port = null;
            return false;
        }

        using var pendingLoginCts = startNewListener ? new CancellationTokenSource() : null;
        if (startNewListener) _pendingLoginCts = pendingLoginCts;

        using var linkedCts = pendingLoginCts != null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, pendingLoginCts.Token)
            : null;
        var loginToken = linkedCts?.Token ?? cancellationToken;
        
        string authUrl = BuildLoginUrl(authProviderBaseUrl, redirectUri);

        if (startNewListener)
        {
            try
            {
                listener.Prefixes.Add($"http://localhost:{_port}/");
                listener.Start();
            }
            catch (Exception e)
            {
                _logger.Error(e.Message, e);
                _port = null;
                _pendingLoginCts = null;
                return false;
            }
        }

        PlatformHelper.OpenHyperLink(authUrl);

        if (startNewListener)
            try
            {
                using var registration = loginToken.Register(() => listener.Stop());

                // The listener stays open until the login succeeds or the login dialog is closed, so a canceled
                // consent can be retried from the browser.
                while (true)
                {
                    var context = await listener.GetContextAsync();
                    loginToken.ThrowIfCancellationRequested();

                    var response = context.Response;
                    var path = context.Request.Url?.AbsolutePath ?? string.Empty;

                    if (path.Equals(RetryPath, StringComparison.OrdinalIgnoreCase))
                    {
                        Redirect(response, BuildLoginUrl(authProviderBaseUrl, redirectUri));
                        continue;
                    }

                    if (!path.Equals(CallbackPath, StringComparison.OrdinalIgnoreCase))
                    {
                        response.StatusCode = 404;
                        response.KeepAlive = false;
                        response.Close();
                        continue;
                    }

                    var query = HttpUtility.ParseQueryString(context.Request.Url!.Query);
                    var code = query["code"];
                    var state = query["state"];
                    var error = query["error"];
                    var isStep1 = state != null && state == _state;
                    var isStep2 = state != null && state == _offlineState;

                    if (!isStep1 && !isStep2)
                    {
                        _logger.Error("Invalid login callback (state mismatch)");
                        await WriteLoginPageAsync(response, 400, "This sign-in link has expired",
                            "Start the sign-in again, or return to OneWare Studio.", canRetry: true);
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(error) || string.IsNullOrWhiteSpace(code))
                    {
                        if (error == "access_denied")
                        {
                            _logger.Log("Sign-in to OneWare Cloud was canceled in the browser.");
                            await WriteLoginPageAsync(response, 200, "Sign-in canceled",
                                "OneWare Studio was not connected. You can sign in again or close this tab.",
                                canRetry: true);
                        }
                        else
                        {
                            _logger.Error(
                                $"Authentication error ({(isStep1 ? "step 1" : "step 2 offline upgrade")}): {SanitizeForLog(error ?? "missing code")}");
                            await WriteLoginPageAsync(response, 400, "Sign-in failed",
                                "OneWare Cloud could not complete the sign-in. Try again, or close this tab.",
                                canRetry: true);
                        }

                        continue;
                    }

                    if (isStep1)
                    {
                        await ExchangeCodeForTokensAsync(code, authProviderBaseUrl, redirectUri,
                            persistTokens: false, clientIdOverride: "Empty");

                        Redirect(response, BuildOfflineConsentUrl(authProviderBaseUrl, redirectUri));
                        continue;
                    }

                    if (!await ExchangeCodeForTokensAsync(code, authProviderBaseUrl, redirectUri,
                            persistTokens: true, codeVerifierOverride: _offlineCodeVerifier))
                    {
                        await WriteLoginPageAsync(response, 500, "Sign-in failed",
                            "OneWare Studio could not store the sign-in. Try again, or close this tab.", canRetry: true);
                        continue;
                    }

                    var cloudHost = _settingService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareCloudHostKey)
                        .TrimEnd('/');
                    Redirect(response, $"{cloudHost}/");

                    return true;
                }
            }
            catch (HttpListenerException) when (loginToken.IsCancellationRequested)
            {
                // Listener was stopped due to cancellation, ignore
            }
            catch (ObjectDisposedException) when (loginToken.IsCancellationRequested)
            {
                // Listener was stopped due to cancellation, ignore
            }
            catch (OperationCanceledException) when (loginToken.IsCancellationRequested)
            {
                // Login was cancelled, ignore
            }
            finally
            {
                listener.Stop();

                if (ReferenceEquals(_pendingLoginCts, pendingLoginCts))
                {
                    _pendingLoginCts = null;
                    _port = null;
                    _codeVerifier = null;
                    _state = null;
                    _offlineCodeVerifier = null;
                    _offlineState = null;
                }
            }

        return startNewListener;
    }

    private const string CallbackPath = "/callback";
    private const string RetryPath = "/retry";

    /// <summary>Step 1: sign in with the minimal client. An existing browser session is reused.</summary>
    private string BuildLoginUrl(string authProviderBaseUrl, string redirectUri)
    {
        _codeVerifier = GenerateCodeVerifier();
        _state = GenerateState();

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = "Empty";
        query["redirect_uri"] = redirectUri;
        query["response_type"] = "code";
        query["scope"] = "openid profile email";
        query["code_challenge"] = GenerateCodeChallenge(_codeVerifier);
        query["code_challenge_method"] = "S256";
        query["state"] = _state;
        return $"{authProviderBaseUrl}/protocol/openid-connect/auth?{query}";
    }

    /// <summary>Step 2: the OneWare Studio client with offline access, which shows the consent screen.</summary>
    private string BuildOfflineConsentUrl(string authProviderBaseUrl, string redirectUri)
    {
        _offlineCodeVerifier = GenerateCodeVerifier();
        _offlineState = GenerateState();

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = "OneWareStudio";
        query["redirect_uri"] = redirectUri;
        query["response_type"] = "code";
        query["scope"] = "openid profile email offline_access";
        query["code_challenge"] = GenerateCodeChallenge(_offlineCodeVerifier);
        query["code_challenge_method"] = "S256";
        query["state"] = _offlineState;
        query["prompt"] = "consent";
        return $"{authProviderBaseUrl}/protocol/openid-connect/auth?{query}";
    }

    private static void Redirect(HttpListenerResponse response, string url)
    {
        response.Redirect(url);
        response.KeepAlive = false;
        response.Close();
    }

    private static async Task WriteLoginPageAsync(HttpListenerResponse response, int statusCode, string title,
        string message, bool canRetry)
    {
        var actions = canRetry
            ? $"""
               <div class="actions">
                 <a class="primary" href="{RetryPath}">Sign in again</a>
               </div>
               """
            : string.Empty;
        var html = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{WebUtility.HtmlEncode(title)}} - OneWare Studio</title>
              <style>
                body { margin: 0; min-height: 100vh; display: flex; align-items: center; justify-content: center;
                       background: #05080d; color: #e5e7eb; font-family: system-ui, -apple-system, "Segoe UI", sans-serif; }
                .card { max-width: 440px; margin: 24px; padding: 32px; background: #0b1017; border: 1px solid #1f2937;
                        border-radius: 12px; }
                h1 { margin: 0 0 12px; font-size: 24px; }
                p { margin: 0; color: #cbd5e1; line-height: 1.6; }
                .actions { display: flex; gap: 12px; margin-top: 24px; flex-wrap: wrap; }
                a { flex: 1; min-width: 160px; padding: 12px 16px; border-radius: 8px; text-align: center; font-weight: 600;
                    text-decoration: none; color: #cbd5e1; border: 1px solid #374151; }
                a.primary { background: #00c8aa; border-color: #00c8aa; color: #041311; }
              </style>
            </head>
            <body>
              <div class="card">
                <h1>{{WebUtility.HtmlEncode(title)}}</h1>
                <p>{{WebUtility.HtmlEncode(message)}}</p>
                {{actions}}
              </div>
            </body>
            </html>
            """;

        var bytes = Encoding.UTF8.GetBytes(html);
        response.StatusCode = statusCode;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        response.KeepAlive = false;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    private async Task<bool> ExchangeCodeForTokensAsync(string code, string authProviderBaseUrl, string redirectUri,
        bool persistTokens = true, string? codeVerifierOverride = null, string? clientIdOverride = null)
    {
        try
        {
            var tokenEndpoint = $"{authProviderBaseUrl}/protocol/openid-connect/token";
            var usedCodeVerifier = codeVerifierOverride ?? _codeVerifier;
            var clientId = clientIdOverride ?? "OneWareStudio";

            var request = new RestRequest(tokenEndpoint, Method.Post);
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("grant_type", "authorization_code");
            request.AddParameter("client_id", clientId);
            request.AddParameter("code", code);
            request.AddParameter("redirect_uri", redirectUri);
            request.AddParameter("code_verifier", usedCodeVerifier);

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
                    return false;
                }

                if (persistTokens)
                {
                    if (string.IsNullOrWhiteSpace(refreshToken))
                    {
                        _logger.Error("Refresh token not found in step-2 response");
                        return false;
                    }

                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await SaveCredentialsAsync(accessToken, refreshToken);
                        _settingService.Save(_paths.SettingsPath);
                    });
                }
                else
                {
                    var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
                    var userId = jwtToken.Claims.FirstOrDefault(x => x.Type == "sub")?.Value;
                    if (userId != null)
                        _jwtBearerTokenCache[userId] = jwtToken;
                }

                return true;
            }

            _logger.Error($"Failed to exchange code for tokens: {response.StatusCode} - {SanitizeForLog(response.Content)}");
            return false;
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return false;
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
            refreshToken = await Task.Run(() =>
            {
                var store = CredentialManager.Create("oneware");
                return store.Get(OneWareCloudIntegrationModule.CredentialStore, userId)?.Password;
            });
        }

        return refreshToken;
    }

    private static bool ShouldLogoutAfterTokenRefreshFailure(HttpStatusCode status)
    {
        return status is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
    }

    private static string SanitizeForLog(string? value)
    {
        return value?.Replace("\r", string.Empty).Replace("\n", string.Empty) ?? string.Empty;
    }


    private class LoginModel
    {
        [JsonPropertyName("email")] public required string Email { get; set; }

        [JsonPropertyName("password")] public required string Password { get; set; }
    }
}
