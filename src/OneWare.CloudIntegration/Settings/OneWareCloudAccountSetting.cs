using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Media;
using GitCredentialManager;
using OneWare.CloudIntegration.Dto;
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

    public OneWareCloudAccountSetting() : base(string.Empty)
    {
        Control = new OneWareCloudAccountSettingViewModel(this);
        _value = string.Empty;
    }

    // Will be the User ID
    public override object Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                IsLoggedIn = !string.IsNullOrWhiteSpace(value.ToString());
                _ = ResolveAsync();
            }
        }
    }

    public string? UserId => Value?.ToString();

    public IImage? Image
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsLoggedIn
    {
        get;
        set => SetProperty(ref field, value);
    }

    public CurrentUserDto? CurrentUser
    {
        get;
        set => SetProperty(ref field, value);
    }

    public async Task ResolveAsync()
    {
        var loginService = ContainerLocator.Container.Resolve<OneWareCloudLoginService>();

        try
        {
            Image = null;

            if (string.IsNullOrEmpty(UserId)) return;

            var (jwt, status) = await loginService.GetJwtTokenAsync(UserId);

            if (jwt == null)
            {
                if (status == HttpStatusCode.Unauthorized)
                {
                    loginService.Logout(UserId);
                    Value = string.Empty;
                    return;
                }
            }

            var request = new RestRequest("/api/users/current");
            request.AddHeader("Authorization", $"Bearer {jwt}");

            var response = await loginService.GetRestClient().ExecuteGetAsync(request);
            CurrentUser = JsonSerializer.Deserialize<CurrentUserDto>(response.Content!, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;

            await ContainerLocator.Container.Resolve<OneWareCloudNotificationService>().ConnectAsync();
            
            var httpService = ContainerLocator.Container.Resolve<IHttpService>();
            
            if (CurrentUser.AvatarUrl != null)
            {
                Image = await httpService.DownloadImageAsync(CurrentUser.AvatarUrl);
                //TODO Move this to somewhere else
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}