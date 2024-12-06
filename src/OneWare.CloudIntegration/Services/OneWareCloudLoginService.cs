using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using GitCredentialManager;
using OneWare.Essentials.Services;
using RestSharp;

namespace OneWare.CloudIntegration.Services;

public class OneWareCloudLoginService(ILogger logger, ISettingsService settingService)
{
    public string? GetLoggedInToken()
    {
        var email = settingService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareAccountEmailKey);

        return GetToken(email);
    }
    
    public string? GetToken(string? email)
    {
        var store = CredentialManager.Create("oneware");

        try
        {
            var cred = store.Get(OneWareCloudIntegrationModule.CredentialStore, email);
            return cred?.Password;
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
            return null;
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
            
                if (token != null)
                {
                    var store = CredentialManager.Create("oneware");
                    store.AddOrUpdate(OneWareCloudIntegrationModule.CredentialStore, email, token);
                
                    settingService.SetSettingValue(OneWareCloudIntegrationModule.OneWareAccountEmailKey, email);

                    return true;
                }
            }
            
            return false;
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
        }

        return false;
    }
    
    private class LoginModel
    {
        [JsonPropertyName("email")]
        public string Email { get; set; }
        
        [JsonPropertyName("password")]
        public string Password { get; set; }
    }
}