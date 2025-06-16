using System.Runtime.InteropServices;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.Settings;
using OneWare.CloudIntegration.ViewModels;
using OneWare.CloudIntegration.Views;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Prism.Modularity;

namespace OneWare.CloudIntegration;

public class OneWareCloudIntegrationModule
{
    public const string OneWareCloudHostKey = "General_OneWareCloud_Host";
    public const string OneWareAccountEmailKey = "General_OneWareCloud_AccountEmail";
    public const string CredentialStore = "https://cloud.one-ware.com";
    
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<OneWareCloudAccountSettingViewModel>();
        containerRegistry.RegisterSingleton<OneWareCloudLoginService>();
        containerRegistry.RegisterSingleton<OneWareCloudNotificationService>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Environment.SetEnvironmentVariable("GCM_CREDENTIAL_STORE", "secretservice");
        
        var settingsService = containerProvider.Resolve<ISettingsService>();
        settingsService.RegisterSetting("General", "OneWare Cloud", OneWareCloudHostKey, new TextBoxSetting("Host", "https://cloud.one-ware.com", "https://cloud.one-ware.com"));
        settingsService.RegisterCustom("General", "OneWare Cloud", OneWareAccountEmailKey, new OneWareCloudAccountSetting());
        
        var windowService = containerProvider.Resolve<IWindowService>();
        windowService.RegisterUiExtension("MainWindow_BottomRightExtension", new UiExtension(x =>
            new CloudIntegrationMainWindowBottomRightExtension()
            {
                DataContext = containerProvider.Resolve<CloudIntegrationMainWindowBottomRightExtensionViewModel>()
            }));
    }
}