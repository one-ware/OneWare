using System.Net;
using System.Reactive.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using Microsoft.AspNetCore.SignalR.Client;
using OneWare.CloudIntegration.Dto;
using OneWare.CloudIntegration.Settings;
using OneWare.Essentials.Services;
using RestSharp;

namespace OneWare.CloudIntegration.Services;

public class OneWareCloudCurrentAccountService : ObservableObject
{
    private readonly OneWareCloudAccountSetting _accountSetting;
    private readonly OneWareCloudLoginService _loginService;
    
    public OneWareCloudCurrentAccountService(OneWareCloudAccountSetting accountSetting, OneWareCloudLoginService loginService, OneWareCloudNotificationService notificationService)
    {
        _accountSetting = accountSetting;
        _loginService = loginService;

        accountSetting.WhenValueChanged(x => x.Value)
            .Subscribe(x => _ = ResolveAsync());
        
        Observable.FromEventPattern<HubConnectionState>(notificationService, nameof(notificationService.ConnectionStateChanged))
            .Subscribe(x =>
            {
                if (notificationService.ConnectionState == HubConnectionState.Connected)
                {
                    IsConnected = true;
                    _ = UpdateBalanceAsync();
                }
                else
                {
                    IsConnected = false;
                }
            });
        
        SubscribeToHub(notificationService);
    }
    
    public bool IsConnected
    {
        get;
        set => SetProperty(ref field, value);
    }

    public UserBalanceDto? CurrentBalance
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string MonthlyIncludedCreditsValue =>
        $"{((CurrentUser?.UserPlan.IncludedMonthlyCredits) - CurrentBalance?.IncludedMonthlyCreditsUsed ?? 0)}";
    
    public CurrentUserDto? CurrentUser
    {
        get;
        set => SetProperty(ref field, value);
    }
    
    public string? UserId => _accountSetting.Value.ToString();

    private async Task ResolveAsync()
    {
        try
        {
            _accountSetting.Image = null;
            _accountSetting.Email = null;
            CurrentUser = null;
            CurrentBalance = null;

            if (string.IsNullOrEmpty(UserId)) return;

            var (jwt, status) = await _loginService.GetJwtTokenAsync(UserId);

            if (jwt == null)
            {
                if (status == HttpStatusCode.Unauthorized)
                {
                    _loginService.Logout(UserId);
                    _accountSetting.Value = string.Empty;
                    return;
                }
            }

            var request = new RestRequest("/api/users/current");
            request.AddHeader("Authorization", $"Bearer {jwt}");

            var response = await _loginService.GetRestClient().ExecuteGetAsync(request);
            CurrentUser = JsonSerializer.Deserialize<CurrentUserDto>(response.Content!, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _accountSetting.Email = CurrentUser?.Email ?? string.Empty;

            await UpdateBalanceAsync();

            await ContainerLocator.Container.Resolve<OneWareCloudNotificationService>().ConnectAsync();
            
            var httpService = ContainerLocator.Container.Resolve<IHttpService>();
            
            if (CurrentUser?.AvatarUrl != null)
            {
                _accountSetting.Image = await httpService.DownloadImageAsync(CurrentUser.AvatarUrl);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task UpdateBalanceAsync()
    {
        var (jwt, status) = await _loginService.GetLoggedInJwtTokenAsync();
        var request = new RestRequest("/api/credits/balance");
        request.AddHeader("Authorization", $"Bearer {jwt}");

        var response = await _loginService.GetRestClient().ExecuteGetAsync(request);
        CurrentBalance = JsonSerializer.Deserialize<UserBalanceDto>(response.Content!, new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        })!;
    }
    
    private void SubscribeToHub(OneWareCloudNotificationService service)
    {
        service.SubscribeToHubMethod<UserBalanceDto>("Balance_Updated", creditBalance =>
        {
            CurrentBalance = creditBalance;
        });
    }
}