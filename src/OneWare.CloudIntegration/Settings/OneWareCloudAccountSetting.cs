using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Media;
using GitCredentialManager;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.ViewModels;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using RestSharp;

namespace OneWare.CloudIntegration.Settings;

public class OneWareCloudAccountSetting : CustomSetting
{
    private readonly OneWareCloudLoginService _oneWareCloudLoginService;
    private readonly OneWareCloudNotificationService _oneWareCloudNotificationService;
    private readonly OneWareCloudAccountSettingViewModel _oneWareCloudAccountSettingViewModel;
    private readonly IHttpService _httpService;

    private object _value;
    private IImage? _image;

    public OneWareCloudAccountSetting(OneWareCloudLoginService oneWareCloudLoginService, 
                                      OneWareCloudNotificationService oneWareCloudNotificationService,
                                      OneWareCloudAccountSettingViewModel oneWareCloudAccountSettingViewModel,
                                      IHttpService httpService) : base(string.Empty)
    {
        _oneWareCloudAccountSettingViewModel = oneWareCloudAccountSettingViewModel;        
        _value = string.Empty;
        _oneWareCloudLoginService = oneWareCloudLoginService;
        _oneWareCloudNotificationService = oneWareCloudNotificationService;
        _httpService = httpService;
        Control = _oneWareCloudAccountSettingViewModel.Setting = this;        
    }

    
    public override object Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                OnPropertyChanged(nameof(IsLoggedIn));
                OnPropertyChanged(nameof(Email));
                _ = ResolveAsync();
            }
        }
    }

    public IImage? Image
    {
        get => _image;
        set => SetProperty(ref _image, value);
    }

    public bool IsLoggedIn => !string.IsNullOrEmpty(Value.ToString());

    public string? Email => IsLoggedIn ? Value.ToString() : "Not logged in";

    private async Task ResolveAsync()
    {

        Image = null;

        if (string.IsNullOrEmpty(Value.ToString()) || Email == null) return;

        var (jwt, status) = await _oneWareCloudLoginService.GetJwtTokenAsync(Email);

        if (jwt == null)
        {
            if (status == HttpStatusCode.Unauthorized)
            {
                _oneWareCloudLoginService.Logout(Email);
                Value = string.Empty;
                return;
            }
        }

        var request = new RestRequest("/api/users/me");
        request.AddHeader("Authorization", $"Bearer {jwt}");

        var response = await _oneWareCloudLoginService.GetRestClient().ExecuteGetAsync(request);
        var data = JsonSerializer.Deserialize<JsonNode>(response.Content!)!;

        var avatarUrl = data["avatarUrl"]?.GetValue<string>();
        
        if (avatarUrl != null)
        {
            Image = await _httpService.DownloadImageAsync(avatarUrl);

            Console.WriteLine(avatarUrl);

            //TODO Move this to somewhere else
            await _oneWareCloudNotificationService.ConnectAsync();
        }
    }
}