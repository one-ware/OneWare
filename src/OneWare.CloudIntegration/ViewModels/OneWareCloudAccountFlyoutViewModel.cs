using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.Settings;

namespace OneWare.CloudIntegration.ViewModels;

public class OneWareCloudAccountFlyoutViewModel : ObservableObject
{
    private const string RegisterPath = "/account/register";
    private const string ManageAccountPath = "/account/manage";
    private const string ChangeAddressPath = "/account/manage/changeAddress";

    public OneWareCloudAccountFlyoutViewModel(
        OneWareCloudAccountSetting setting,
        OneWareCloudCurrentAccountService accountService)
    {
        CurrentAccountService = accountService;

        const string baseUrl = OneWareCloudIntegrationModule.CredentialStore;
        SettingViewModel = new OneWareCloudAccountSettingViewModel(setting);

        accountService.WhenValueChanged(x => x.CurrentUser).Subscribe(x =>
        {
            if (x == null)
                Url = $"{baseUrl}{RegisterPath}";
            else
                Url = $"{baseUrl}{ManageAccountPath}";
        });

        ChangeAddressLink = $"{baseUrl}{ChangeAddressPath}";
    }

    public OneWareCloudCurrentAccountService CurrentAccountService { get; }

    public OneWareCloudAccountSettingViewModel SettingViewModel { get; }

    public string? Url
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string ChangeAddressLink { get; }

    public async Task OpenFeedbackDialogAsync(Control parent)
    {
        await OneWareCloudIntegrationModule.OpenFeedbackDialogAsync();
    }
}