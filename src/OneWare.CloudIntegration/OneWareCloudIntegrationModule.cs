using System;
using System.Runtime.InteropServices;
using Autofac;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.Settings;
using OneWare.CloudIntegration.ViewModels;
using OneWare.Essentials.Services;

namespace OneWare.CloudIntegration;

public class OneWareCloudIntegrationModule : Module
{
    public const string Host = "http://localhost:5140";
    public const string OneWareAccountEmailKey = "General_OneWareCloud_AccountEmail";
    public const string CredentialStore = "https://cloud.one-ware.com";

    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<OneWareCloudAccountSettingViewModel>()
               .SingleInstance();

        builder.RegisterType<OneWareCloudLoginService>()
               .SingleInstance();

        builder.RegisterType<OneWareCloudNotificationService>()
               .SingleInstance();
    }

    public static void InitializeSettings(IComponentContext context)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Environment.SetEnvironmentVariable("GCM_CREDENTIAL_STORE", "secretservice");
        }

        var settingsService = context.Resolve<ISettingsService>();
        settingsService.RegisterCustom("General", "OneWare Cloud", OneWareAccountEmailKey, new OneWareCloudAccountSetting());
    }
}
