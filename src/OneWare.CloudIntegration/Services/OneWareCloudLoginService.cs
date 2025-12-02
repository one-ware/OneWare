using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using GitCredentialManager;
using OneWare.Essentials.Services;
using Prism.Ioc;
using RestSharp;

namespace OneWare.CloudIntegration.Services;

public sealed class OneWareCloudLoginService
{
    private readonly Dictionary<string, JwtSecurityToken> _jwtTokenCache = new();
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    private readonly ILogger _logger;
    private readonly ISettingsService _settingService;
    private readonly IHttpService _httpService;
    private readonly string _tokenPath;
    
    public OneWareCloudLoginService(ILogger logger, ISettingsService settingService, IHttpService httpService, IPaths paths)
    {
        _logger = logger;
        _settingService = settingService;
        _httpService = httpService;
        _tokenPath = Path.Combine(paths.AppDataDirectory, "Cloud");
        
        settingService.GetSettingObservable<string>(OneWareCloudIntegrationModule.OneWareCloudHostKey)
            .Skip(1)
            .Subscribe(x =>
            {
                Logout(settingService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareAccountEmailKey));
            });
    }
    
    public RestClient GetRestClient()
    {
        var baseUrl = _settingService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareCloudHostKey);
        return new RestClient(_httpService.HttpClient, new RestClientOptions(baseUrl));
    }
    
    public Task<(string? token, HttpStatusCode status)> GetLoggedInJwtTokenAsync()
    {
        var email = _settingService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareAccountEmailKey);

        return GetJwtTokenAsync(email);
    }

    /// <summary>
    /// Gives a JWT Token that has at least 5 minutes left before expiration
    /// TODO Change this to return the full token implementation
    /// </summary>
    public async Task<(string? token, HttpStatusCode status)> GetJwtTokenAsync(string email)
    {
        await _semaphoreSlim.WaitAsync();
        
        try
        {
            if (string.IsNullOrWhiteSpace(email)) return (null, HttpStatusCode.Unauthorized);
            
            _jwtTokenCache.TryGetValue(email, out var existingToken);

            if (existingToken?.ValidTo > DateTime.UtcNow.AddMinutes(5))
            {
                return (existingToken.RawData, HttpStatusCode.NoContent);
            }

            var (result, status) = await RefreshAsync(email);
            
            if (!result) return (null, status);

            if (!_jwtTokenCache.TryGetValue(email, out var regeneratedToken)) return (null, status);

            return (regeneratedToken.RawData, status);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public async Task<(bool success, HttpStatusCode status)> RefreshAsync(string email)
    {
        try
        {
            string? refreshToken = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var tokenPath = Path.Combine(_tokenPath, $"{email}.bin");
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

                var cred = store.Get(OneWareCloudIntegrationModule.CredentialStore, email);
                refreshToken = cred?.Password;
            }
            
            if (refreshToken == null) 
                return (false, HttpStatusCode.Unauthorized);

            var request = new RestRequest("/api/auth/refresh");
            request.AddJsonBody(new RefreshModel()
            {
                RefreshToken = refreshToken
            });
            var response = await GetRestClient().ExecutePostAsync(request);

            if (response.IsSuccessful)
            {
                var data = JsonSerializer.Deserialize<JsonNode>(response.Content!)!;
                var token = data["token"]?.GetValue<string>();
                refreshToken = data["refreshToken"]?.GetValue<string>();

                if (token == null || refreshToken == null) 
                    throw new Exception("Token or refresh token not found");

                SaveCredentials(email, token, refreshToken);

                return (true, response.StatusCode);
            }

            return (false, response.StatusCode);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return (false, HttpStatusCode.NoContent);
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

                SaveCredentials(email, token, refreshToken);

                return (true, HttpStatusCode.OK);
            }
            else if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return (false, response.StatusCode);
            }

            return (false, response.StatusCode);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }

        return (false, HttpStatusCode.NoContent);
    }

    public void Logout(string email)
    {
        _settingService.SetSettingValue(OneWareCloudIntegrationModule.OneWareAccountEmailKey, "");
        _ = ContainerLocator.Container.Resolve<OneWareCloudNotificationService>().DisconnectAsync();
        
        try
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var tokenPath = Path.Combine(_tokenPath, $"{email}.bin");
                if(File.Exists(tokenPath)) 
                    File.Delete(tokenPath);
            }
            else
            {
                var store = CredentialManager.Create("oneware");
                store.Remove(OneWareCloudIntegrationModule.CredentialStore, email);
            }
            _jwtTokenCache.Remove(email);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }
    
    public async Task<bool> SendFeedbackAsync(string category, string message)
    {
        try
        {
            RestRequest? request;
            (string? jwt, HttpStatusCode status) = await GetLoggedInJwtTokenAsync();
            if (jwt == null)
            {
                request = new RestRequest("/api/feedback/anonymous");
            }
            else
            {
                request = new RestRequest("/api/feedback");
                request.AddHeader("Authorization", $"Bearer {jwt}");
            }
            
            request.AddHeader("Accept", "application/json");
            request.AddJsonBody(new
            {
                Category = category,
                Message = message
            });
            
            var response = await GetRestClient().ExecutePostAsync(request);
            return response.IsSuccessful;
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }

        return false;
    }

    private void SaveCredentials(string email, string token, string refreshToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        _jwtTokenCache[email] = jwt;

        try
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Directory.CreateDirectory(_tokenPath);
                var tokenPath = Path.Combine(_tokenPath, $"{email}.bin");
                
                var plaintext = Encoding.UTF8.GetBytes(refreshToken);
                var encrypted = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(tokenPath, encrypted);
            }
            else
            {
                var store = CredentialManager.Create("oneware");
                store.AddOrUpdate(OneWareCloudIntegrationModule.CredentialStore, email, refreshToken);
            }
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
        
        _settingService.SetSettingValue(OneWareCloudIntegrationModule.OneWareAccountEmailKey, email);
    }

    private class LoginModel
    {
        [JsonPropertyName("email")] public string Email { get; set; }

        [JsonPropertyName("password")] public string Password { get; set; }
    }

    private class RefreshModel
    {
        [JsonPropertyName("refreshToken")] public string RefreshToken { get; set; }
    }
}