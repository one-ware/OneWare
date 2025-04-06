using System.Runtime.InteropServices;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.Settings;
using OneWare.CloudIntegration.ViewModels;
using OneWare.Essentials.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.CloudIntegration;

public class OneWareCloudIntegrationModule : IModule
{
    public const string Host = "http://localhost:5140";
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
        
        containerProvider.Resolve<ISettingsService>().RegisterCustom("General", "OneWare Cloud", OneWareAccountEmailKey, new OneWareCloudAccountSetting());
    }
}