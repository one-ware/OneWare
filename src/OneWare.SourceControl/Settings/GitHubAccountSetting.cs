using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Media;
using GitCredentialManager;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Settings;
using OneWare.SourceControl.ViewModels;
using RestSharp;
using Autofac;

namespace OneWare.SourceControl.Settings;

public class GitHubAccountSetting : CustomSetting
{
    private object _value;
    private IImage? _image;

    // Constructor injection for IHttpService
    private readonly IHttpService _httpService;

    // Constructor to inject IHttpService
    public GitHubAccountSetting(IHttpService httpService) : base(string.Empty)
    {
        _httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
        Control = new GitHubAccountSettingViewModel(this);
        _value = string.Empty;
    }

    public override object Value
    {
        get => _value;
        set
        {
            SetProperty(ref _value, value);
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(Username));
            _ = ResolveAsync();
        }
    }

    public IImage? Image
    {
        get => _image;
        set => SetProperty(ref _image, value);
    }

    public bool IsLoggedIn => !string.IsNullOrEmpty(Value.ToString());

    public string? Username => IsLoggedIn ? Value.ToString() : "Not logged in";

    private async Task ResolveAsync()
    {
        Image = null;

        if (string.IsNullOrEmpty(Value.ToString())) return;

        var store = CredentialManager.Create("oneware");

        var cred = store.Get("https://github.com", Value.ToString());

        if (cred == null) return;

        var client = new RestClient("https://api.github.com");
        var request = new RestRequest("/user");
        request.AddHeader("Authorization", $"Bearer {cred.Password}");

        var response = await client.ExecuteGetAsync(request);
        var data = JsonSerializer.Deserialize<JsonNode>(response.Content!)!;

        var avatarUrl = data["avatar_url"]?.GetValue<string>();

        if (avatarUrl != null)
            Image = await _httpService.DownloadImageAsync(avatarUrl);
        else
        {
            // Bad credentials, logout
            if (data["status"]?.GetValue<string>() == "401")
            {
                store.Remove("https://github.com", Value.ToString());
                Value = string.Empty;
            }
        }
    }
}
