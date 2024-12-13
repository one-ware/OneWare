using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Media;
using GitCredentialManager;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.ViewModels;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Prism.Ioc;
using RestSharp;

namespace OneWare.CloudIntegration.Settings;

public class OneWareCloudAccountSetting : CustomSetting
{
    private object _value;
    
    private IImage? _image;

    public OneWareCloudAccountSetting() : base(string.Empty)
    {
        Control = new OneWareCloudAccountSettingViewModel(this);
        _value = string.Empty;
    }

    public override object Value {
        get => _value;
        set
        {
            SetProperty(ref _value, value);
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(Email));
            _ = ResolveAsync();
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
        var loginService = ContainerLocator.Container.Resolve<OneWareCloudLoginService>();
        
        Image = null;
        
        if(string.IsNullOrEmpty(Value.ToString()) || Email == null) return;

        var (jwt, status) = await loginService.GetJwtTokenAsync(Email);

        if (jwt == null)
        {
            if (status == HttpStatusCode.Unauthorized)
            {
                loginService.Logout(Email);
                Value = string.Empty;
                return;
            }
        }

        var client = new RestClient(OneWareCloudIntegrationModule.Host);
        var request = new RestRequest("/api/user/data");
        request.AddHeader("Authorization", $"Bearer {jwt}");
        
        var response = await client.ExecuteGetAsync(request);
        var data = JsonSerializer.Deserialize<JsonNode>(response.Content!)!;
        
        var avatarUrl = data["avatarUrl"]?.GetValue<string>();
        
        var httpService = ContainerLocator.Container.Resolve<IHttpService>();
        
        if(avatarUrl != null)
            Image = await httpService.DownloadImageAsync(avatarUrl);
    }
}