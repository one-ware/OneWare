using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.Settings;

namespace OneWare.CloudIntegration.ViewModels;

public class OneWareCloudAccountFlyoutViewModel : ObservableObject
{
    private bool _Initialized;
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
        creditBalanceSetting.SubscribeToHub(cloudNotificationService, loginService);
        Information.Add(creditBalanceSetting);
        
        setting.WhenValueChanged(x => x.IsLoggedIn)
            .Subscribe(value =>
            {
                if (value)
                {
                    _ = creditBalanceSetting.OnLoginAsync(loginService);
                    UrlLabel = "Manage your account";
                    Url = $"{baseUrl}{ManageAccountPath}";
                }
                else
                {
                    creditBalanceSetting.Value = string.Empty;
                    UrlLabel = "Create an account";
                    Url = $"{baseUrl}{RegisterPath}";
                }

                //the account information are only visible, if the user is logged in
                foreach (IOneWareCloudAccountFlyoutSetting item in Information)
                    item.IsVisible = value;
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