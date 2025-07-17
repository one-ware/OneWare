using System.Text.Json;
using System.Text.Json.Nodes;
using GitCredentialManager;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using RestSharp;

namespace OneWare.SourceControl.LoginProviders;

public class GithubLoginProvider(ISettingsService settingsService, ILogger logger) : ILoginProvider
{
    public string Name => "GitHub";
    
    public string Host => "https://github.com";

    public string GenerateLink =>
        "https://github.com/settings/tokens/new?description=OneWare%20Studio%20GitHub%20integration%20plugin&scopes=repo%2Cgist%2Cread%3Aorg%2Cworkflow%2Cread%3Auser%2Cuser%3Aemail";
    
    public async Task<bool> LoginAsync(string password)
    {
        try
        {
            var client = new RestClient("https://api.github.com");
            var request = new RestRequest("/user");
            request.AddHeader("Authorization", $"Bearer {password}");
        
            var response = await client.ExecuteGetAsync(request);
            var data = JsonSerializer.Deserialize<JsonNode>(response.Content!)!;

            var username = data["login"]?.GetValue<string>();
        
            if (username != null)
            {
                var store = CredentialManager.Create("oneware");
                store.AddOrUpdate(Host, username, password);
                
                settingsService.SetSettingValue(SourceControlModule.GitHubAccountNameKey, username);
                
                return true;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
        }
        return false;
    }
}