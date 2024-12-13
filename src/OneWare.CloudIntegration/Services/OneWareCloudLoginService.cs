using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using GitCredentialManager;
using OneWare.Essentials.Services;
using RestSharp;

namespace OneWare.CloudIntegration.Services;

public class OneWareCloudLoginService(ILogger logger, ISettingsService settingService)
{
    private Dictionary<string, JwtToken> _jwtTokenCache = new();

    public Task<string?> GetLoggedInJwtTokenAsync()
    {
        var email = settingService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareAccountEmailKey);

        return GetJwtTokenAsync(email);
    }

    /// <summary>
    /// Gives a JWT Token that has at least 5 minutes left before expiration
    /// </summary>
    public async Task<string?> GetJwtTokenAsync(string email)
    {
        _jwtTokenCache.TryGetValue(email, out var existingToken);

        if (existingToken?.Expiration > DateTime.Now.AddMinutes(5))
        {
            return existingToken.Token;
        }

        var result = await RefreshAsync(email);
        if (!result) return null;

        if (!_jwtTokenCache.TryGetValue(email, out var regeneratedToken)) return null;

        return regeneratedToken.Token;
    }

    public async Task<bool> RefreshAsync(string email)
    {
        try
        {
            var store = CredentialManager.Create("oneware");

            var cred = store.Get(OneWareCloudIntegrationModule.CredentialStore, email);
            var refreshToken = cred?.Password;

            if (refreshToken == null) throw new InvalidOperationException("Refresh token not found");

            var client = new RestClient(OneWareCloudIntegrationModule.Host);
            var request = new RestRequest("/api/auth/refresh");
            request.AddJsonBody(new RefreshModel()
            {
                RefreshToken = refreshToken
            });

            var response = await client.ExecutePostAsync(request);

            if (response.IsSuccessful)
            {
                var data = JsonSerializer.Deserialize<JsonNode>(response.Content!)!;
                
                var token = data["token"]?.GetValue<string>();
                refreshToken = data["refreshToken"]?.GetValue<string>();

                if(token == null || refreshToken == null) throw new Exception("Token or refresh token not found");
                
                SaveCredentials(email, token, refreshToken);

                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
            return false;
        }
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        try
        {
            var client = new RestClient(OneWareCloudIntegrationModule.Host);
            var request = new RestRequest("/api/auth/login");
            request.AddJsonBody(new LoginModel
            {
                Email = email,
                Password = password
            });

            var response = await client.ExecutePostAsync(request);

            if (response.IsSuccessful)
            {
                var data = JsonSerializer.Deserialize<JsonNode>(response.Content!)!;

                var token = data["token"]?.GetValue<string>();
                var refreshToken = data["refreshToken"]?.GetValue<string>();

                if(token == null || refreshToken == null) throw new Exception("Token or refresh token not found");
                
                SaveCredentials(email, token, refreshToken);

                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
        }

        return false;
    }

    public void Logout(string email)
    {
        try
        {
            var store = CredentialManager.Create("oneware");
            store.Remove(OneWareCloudIntegrationModule.CredentialStore, email);
            _jwtTokenCache.Remove(email);
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
        }
    }

    private void SaveCredentials(string email, string token, string refreshToken)
    {
        _jwtTokenCache[email] = new JwtToken()
        {
            Token = token,
            Expiration = DateTime.Now.AddMinutes(15)
        };

        var store = CredentialManager.Create("oneware");
        store.AddOrUpdate(OneWareCloudIntegrationModule.CredentialStore, email, refreshToken);

        settingService.SetSettingValue(OneWareCloudIntegrationModule.OneWareAccountEmailKey, email);
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

    private class JwtToken
    {
        public required string Token { get; init; }

        public required DateTime Expiration { get; init; }
    }
}