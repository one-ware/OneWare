using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using Microsoft.AspNetCore.SignalR.Client;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.Settings;

namespace OneWare.CloudIntegration.ViewModels;

public class OneWareCloudAccountFlyoutViewModel : ObservableObject
{
    private const string RegisterPath = "/account/register";
    private const string ManageAccountPath = "/account/manage";

    private string? _urlLabel;
    private string? _url;

    public OneWareCloudAccountFlyoutViewModel(
        OneWareCloudLoginService loginService,
        OneWareCloudNotificationService cloudNotificationService, 
        OneWareCloudAccountSetting setting)
    {
        const string baseUrl = OneWareCloudIntegrationModule.CredentialStore;
        SettingViewModel = new OneWareCloudAccountSettingViewModel(setting);
        
        CreditBalanceSetting creditBalanceSetting = new("Credit balance", (Application.Current?.FindResource("Credit") as IImage)!);
        creditBalanceSetting.SubscribeToHub(cloudNotificationService);
        Information.Add(creditBalanceSetting);
        
       setting.WhenValueChanged(x => x.IsLoggedIn).Subscribe(x =>
        {
            if (!x)
            {
                creditBalanceSetting.Value = string.Empty;
                UrlLabel = "Create an account";
                Url = $"{baseUrl}{RegisterPath}";
            }
            else
            {
                _ = creditBalanceSetting.UpdateBalanceAsync(loginService);
                UrlLabel = "Manage your account";
                Url = $"{baseUrl}{ManageAccountPath}";
            }

            //the account information are only visible, if the user is logged in
            foreach (IOneWareCloudAccountFlyoutSetting item in Information)
                item.IsVisible = x;
        });
       
        Observable.FromEventPattern<HubConnectionState>(cloudNotificationService, nameof(cloudNotificationService.ConnectionStateChanged))
            .Subscribe(x =>
            {
                if (cloudNotificationService.ConnectionState == HubConnectionState.Connected)
                {
                    if (!setting.IsLoggedIn)
                    {
                        creditBalanceSetting.Value = string.Empty;
                        return;
                    }
                    _ = creditBalanceSetting.UpdateBalanceAsync(loginService);
                }
                else
                {
                    creditBalanceSetting.Value = "Not connected";
                }
            });
    }

    public OneWareCloudAccountSettingViewModel SettingViewModel { get; }
    
    public ObservableCollection<IOneWareCloudAccountFlyoutSetting> Information { get; } = [];
    
    public string? UrlLabel
    {
        get => _urlLabel;
        set => SetProperty(ref _urlLabel, value);
    }
    public string? Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }
}

public interface IOneWareCloudAccountFlyoutSetting
{
    string Title { get; }
    string? Value { get; set; }
    bool IsVisible { get; set; }
    IImage? Icon { get; set; }
}