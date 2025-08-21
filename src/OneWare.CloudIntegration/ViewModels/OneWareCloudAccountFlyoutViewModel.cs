using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.Settings;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.SourceControl.Views;
using Prism.Ioc;

namespace OneWare.CloudIntegration.ViewModels;

public class OneWareCloudAccountFlyoutViewModel : ObservableObject
{
    private const string BaseUrl = "https://cloud.one-ware.com";
    private const string RegisterUrl = $"{BaseUrl}/Account/Register";
    private const string ManageAccountUrl = $"{BaseUrl}/Account/Manage";
    
    private string? _urlLabel;
    private string? _url;

    public OneWareCloudAccountFlyoutViewModel(OneWareCloudAccountSetting setting)
    {
        SettingViewModel = new OneWareCloudAccountSettingViewModel(setting);
        
        setting.WhenValueChanged(x => x.IsLoggedIn)
            .Subscribe(value =>
            {
                if (value)
                {
                    UrlLabel = "Manage your account";
                    Url = ManageAccountUrl;
                }
                else
                {
                    UrlLabel = "Create an account";
                    Url = RegisterUrl;
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