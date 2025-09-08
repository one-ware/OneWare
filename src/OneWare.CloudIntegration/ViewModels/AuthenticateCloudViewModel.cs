using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using GitCredentialManager;
using OneWare.CloudIntegration.Services;
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
    private readonly OneWareCloudLoginService _loginService;

    private string? _errorText;
    
    private string _email = string.Empty;
    
    private string _password = string.Empty;

    private bool _isLoading = false;
    
    public AuthenticateCloudViewModel(ISettingsService settingsService, ILogger logger, OneWareCloudLoginService loginService)
    {
        _settingService = settingsService;
        _logger = logger;
        _loginService = loginService;
        
        Title = $"Login to OneWare Cloud";
        
        Description = $"Login to OneWare Cloud";
    }
    
    public string Description { get; }
    
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

        var result = await _loginService.LoginAsync(Email, Password);

        IsLoading = false;

        if (!result.success)
        {
            ErrorText = result.status switch
            {
                0 => "Connection Failed",
                HttpStatusCode.Unauthorized => "Invalid email or password",
                HttpStatusCode.TooManyRequests => "Too many login attempts, please try again later",
                _ => "Unknown error"
            };
            return;
        }
        
        window.Close();
    }
    
    public void Cancel(FlexibleWindow window)
    {
        window.Close();
    }
}