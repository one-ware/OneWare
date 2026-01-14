using System.Collections.ObjectModel;
using System.Net;
using System.Reactive.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using LiveChartsCore.SkiaSharpView.Avalonia;
using Microsoft.AspNetCore.SignalR.Client;
using OneWare.CloudIntegration.Dto;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.Settings;
using RestSharp;

namespace OneWare.CloudIntegration.ViewModels;

public class OneWareCloudAccountFlyoutViewModel : ObservableObject
{
    private const string RegisterPath = "/account/register";
    private const string ManageAccountPath = "/account/manage";
    private const string ChangeAddressPath = "/account/manage/changeAddress";
    
    private readonly OneWareCloudLoginService _cloudLoginService;
    
    public OneWareCloudAccountFlyoutViewModel(
        OneWareCloudLoginService loginService,
        OneWareCloudNotificationService cloudNotificationService, 
        OneWareCloudAccountSetting setting)
    {
        _cloudLoginService = loginService;
        AccountSetting = setting;
        
        const string baseUrl = OneWareCloudIntegrationModule.CredentialStore;
        SettingViewModel = new OneWareCloudAccountSettingViewModel(setting);
        
        setting.WhenValueChanged(x => x.CurrentUser).Subscribe(x =>
        {
            if (x == null)
            {
                CurrentBalance = null;
                Url = $"{baseUrl}{RegisterPath}";
            }
            else
            {
                Url = $"{baseUrl}{ManageAccountPath}";
                _ = UpdateBalanceAsync();
            }
        });
       
        Observable.FromEventPattern<HubConnectionState>(cloudNotificationService, nameof(cloudNotificationService.ConnectionStateChanged))
            .Subscribe(x =>
            {
                if (cloudNotificationService.ConnectionState == HubConnectionState.Connected)
                {
                    IsConnected = true;
                    _ = UpdateBalanceAsync();
                }
                else
                {
                    IsConnected = false;
                }
            });
        
        IsConnected = cloudNotificationService.ConnectionState == HubConnectionState.Connected;
        ChangeAddressLink = $"{baseUrl}{ChangeAddressPath}";

        SubscribeToHub(cloudNotificationService);
    }

    public OneWareCloudAccountSettingViewModel SettingViewModel { get; }

    public OneWareCloudAccountSetting AccountSetting { get; }
    
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
        $"{((AccountSetting.CurrentUser?.UserPlan.IncludedMonthlyCredits) - CurrentBalance?.IncludedMonthlyCreditsUsed ?? 0)}";
    
    public string? Url
    {
        get;
        set => SetProperty(ref field, value);
    }

    public async Task OpenFeedbackDialogAsync(Control parent)
    {
        await OneWareCloudIntegrationModule.OpenFeedbackDialogAsync();
    }

    private async Task UpdateBalanceAsync()
    {
        var (jwt, status) = await _cloudLoginService.GetLoggedInJwtTokenAsync();
        var request = new RestRequest("/api/credits/balance");
        request.AddHeader("Authorization", $"Bearer {jwt}");

        var response = await _cloudLoginService.GetRestClient().ExecuteGetAsync(request);
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
    
    public string ChangeAddressLink { get; }
}