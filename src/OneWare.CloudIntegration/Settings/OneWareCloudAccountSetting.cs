using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Media;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.ViewModels;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using RestSharp;

namespace OneWare.CloudIntegration.Settings;

public class OneWareCloudAccountSetting : CustomSetting
{
    private object _value;
    private IImage? _image;
    private readonly OneWareCloudLoginService _loginService;
    private readonly IHttpService _httpService;

    public OneWareCloudAccountSetting(
        OneWareCloudLoginService loginService,
        IHttpService httpService) : base(string.Empty)
    {
        _loginService = loginService;
        _httpService = httpService;

        Control = new OneWareCloudAccountSettingViewModel(this);
        _value = string.Empty;
    }

    public override object Value
    {
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

    public bool IsLoggedIn => !string.IsNullOrEmpty(Value?.ToString());

    public string? Email => IsLoggedIn ? Value?.ToString() : "Not logged in";

    private async Task ResolveAsync()
    {
        Image = null;

        if (string.IsNullOrEmpty(Value?.ToString()) || Email == null)
            return;

        var (jwt, status) = await _loginService.GetJwtTokenAsync(Email);

        if (jwt == null)
        {
            if (status == HttpStatusCode.Unauthorized)
            {
                _loginService.Logout(Email);
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

        if (avatarUrl != null)
            Image = await _httpService.DownloadImageAsync(avatarUrl);
    }
}
