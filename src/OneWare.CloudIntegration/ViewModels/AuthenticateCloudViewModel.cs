using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using GitCredentialManager;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Ioc;
using RestSharp;

namespace OneWare.CloudIntegration.ViewModels;

public class AuthenticateCloudViewModel : FlexibleWindowViewModelBase
{
    private readonly ISettingsService _settingService;
    private readonly ILogger _logger;

    private string? _errorText;
    
    private string _email = string.Empty;
    
    private string _password = string.Empty;

    private bool _isLoading = false;
    
    public AuthenticateCloudViewModel(ISettingsService settingsService, ILogger logger)
    {
        _settingService = settingsService;
        _logger = logger;
        
        Title = $"Login to OneWare Cloud";
        
        Description = $"Login to OneWare Cloud";
    }
    
    public string Description { get; }
    
    public bool Success { get; private set; }
    
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }
    
    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }
    
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }
    
    public string? ErrorText
    {
        get => _errorText;
        set => SetProperty(ref _errorText, value);
    }

    public async Task LoginAsync(FlexibleWindow window)
    {
        if (string.IsNullOrWhiteSpace(Password)) return;

        IsLoading = true;
        ErrorText = null;

        var result = await PerformLoginAsync();

        IsLoading = false;

        if (!result) return;

        Success = true;
        
        window.Close();
    }

    private async Task<bool> PerformLoginAsync()
    {
        try
        {
            var client = new RestClient(OneWareCloudIntegrationModule.Host);
            var request = new RestRequest("/api/auth/login");
            request.AddJsonBody(new LoginModel
            {
                Email = Email,
                Password = Password
            });
        
            var response = await client.ExecutePostAsync(request);

            if (response.IsSuccessful)
            {
                var data = JsonSerializer.Deserialize<JsonNode>(response.Content!)!;
            
                var token = data["token"]?.GetValue<string>();
            
                if (token != null)
                {
                    var store = CredentialManager.Create("oneware");
                    store.AddOrUpdate(OneWareCloudIntegrationModule.CredentialStore, Email, token);
                
                    _settingService.SetSettingValue(OneWareCloudIntegrationModule.OneWareAccountEmailKey, Email);

                    return true;
                }
            }
            
            ErrorText = "Login unsuccessful!";
            return false;
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            ErrorText = e.Message;
        }

        return false;
    }
    
    public void Cancel(FlexibleWindow window)
    {
        window.Close();
    }
    
    public class LoginModel
    {
        [JsonPropertyName("email")]
        public string Email { get; set; }
        
        [JsonPropertyName("password")]
        public string Password { get; set; }
    }
}