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

    private readonly OneWareCloudAccountSetting _setting;
    
    private IImage? _image;
    private string? _settingLabel;
    private string? _email;
    private string? _urlLabel;
    private string? _url;

    public OneWareCloudAccountFlyoutViewModel(OneWareCloudAccountSetting setting)
    {
        _setting = setting;
        
        setting.WhenValueChanged(x => x.Email)
            .Subscribe(value => Email = value);
        setting.WhenValueChanged(x => x.Image)
            .Subscribe(value => Image = value);
        setting.WhenValueChanged(x => x.IsLoggedIn)
            .Subscribe(value =>
            {
                if (value)
                {
                    UrlLabel = "Manage your account";
                    SettingLabel = "Logout";
                    Url = ManageAccountUrl;
                }
                else
                {
                    UrlLabel = "Create an account";
                    SettingLabel = "Login";
                    Url = RegisterUrl;
                }

                //the account information are only visible, if the user is logged in
                foreach (IOneWareCloudAccountFlyoutSetting item in Information)
                    item.IsVisible = value;
            });
    }

    public ObservableCollection<IOneWareCloudAccountFlyoutSetting> Information { get; } = [];
    
    public string? Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }
    public IImage? Image
    {
        get => _image;
        set => SetProperty(ref _image, value);
    }
    public string? SettingLabel
    {
        get => _settingLabel;
        set => SetProperty(ref _settingLabel, value);
    }
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
    
    public Task HandleAuthAsync(Control owner)
    {
        if (owner is Button btn)
            btn.Flyout?.Hide();

        if (!_setting.IsLoggedIn)
        {
            return Dispatcher.UIThread.InvokeAsync(() => ContainerLocator.Container.Resolve<IWindowService>()
                .ShowDialogAsync(new AuthenticateCloudView()
                {
                    DataContext = ContainerLocator.Container.Resolve<AuthenticateCloudViewModel>()
                }, TopLevel.GetTopLevel(owner) as Window));
        }
        else
        {
            ContainerLocator.Container.Resolve<OneWareCloudLoginService>().Logout(Email!);
            _ = ContainerLocator.Container.Resolve<OneWareCloudNotificationService>().DisconnectAsync();
        
            _setting.Value = string.Empty;

            ContainerLocator.Container.Resolve<ISettingsService>()
                .Save(ContainerLocator.Container.Resolve<IPaths>().SettingsPath);
            
            return Task.CompletedTask;
        }
    }
}

public interface IOneWareCloudAccountFlyoutSetting
{
    string Title { get; }
    string? Value { get; set; }
    bool IsVisible { get; set; }
    IImage? Icon { get; set; }
}