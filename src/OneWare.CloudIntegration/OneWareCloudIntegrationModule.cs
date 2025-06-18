using System.Runtime.InteropServices;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.Settings;
using OneWare.CloudIntegration.ViewModels;
using OneWare.CloudIntegration.Views;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Autofac; // IMPORTANT: Use Autofac.Module

namespace OneWare.CloudIntegration;

// Now inherits from Autofac.Module
public class OneWareCloudIntegrationModule : Autofac.Module
{
    public const string OneWareCloudHostKey = "General_OneWareCloud_Host";
    public const string OneWareAccountEmailKey = "General_OneWareCloud_AccountEmail";
    public const string CredentialStore = "https://cloud.one-ware.com";

    // Dependencies are NOT injected into this module's constructor directly.
    // Instead, you use the 'builder' provided in Load() to register.
    // Logic that needs resolved dependencies should be in classes that ARE injected.

    // This method is called by Autofac to register components
    protected override void Load(ContainerBuilder builder)
    {
        // Register singletons for your services and view models
        builder.RegisterType<OneWareCloudAccountSettingViewModel>().AsSelf().SingleInstance();
        builder.RegisterType<OneWareCloudLoginService>().AsSelf().SingleInstance();
        builder.RegisterType<OneWareCloudNotificationService>().AsSelf().SingleInstance();
        builder.RegisterType<CloudIntegrationMainWindowBottomRightExtensionViewModel>().AsSelf().SingleInstance();
        builder.RegisterType<OneWareCloudAccountSetting>().AsSelf().SingleInstance(); // If you want to inject this specific instance

        // If CloudIntegrationMainWindowBottomRightExtension has dependencies and needs to be resolved by Autofac
        // builder.RegisterType<CloudIntegrationMainWindowBottomRightExtension>().AsSelf();
        // Typically, Avalonia views are instantiated by Avalonia itself, but their DataContexts are often resolved.
    }

    // You no longer have an OnInitialized method from IModule.
    // Initialization logic that depends on resolved services needs to move.
    // Option A: Move to a dedicated initializer service.
    // Option B: Move to the constructor or an initialization method of a service that IS resolved by Autofac.

    // Let's create an interface and implementation for the initialization logic
    public interface ICloudIntegrationInitializer
    {
        void Initialize();
    }

    // This class will contain the logic that was previously in OnInitialized
    public class CloudIntegrationInitializer : ICloudIntegrationInitializer
    {
        private readonly ISettingsService _settingsService;
        private readonly IWindowService _windowService;
        private readonly CloudIntegrationMainWindowBottomRightExtensionViewModel _cloudIntegrationViewModel;
        private readonly OneWareCloudAccountSetting _oneWareCloudAccountSetting;

        // Constructor injection for all necessary services
        public CloudIntegrationInitializer(
            ISettingsService settingsService,
            IWindowService windowService,
            CloudIntegrationMainWindowBottomRightExtensionViewModel cloudIntegrationViewModel,
            OneWareCloudAccountSetting oneWareCloudAccountSetting)
        {
            _settingsService = settingsService;
            _windowService = windowService;
            _cloudIntegrationViewModel = cloudIntegrationViewModel;
            _oneWareCloudAccountSetting = oneWareCloudAccountSetting;
        }

        public void Initialize()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Environment.SetEnvironmentVariable("GCM_CREDENTIAL_STORE", "secretservice");

            _settingsService.RegisterSetting("General", "OneWare Cloud", OneWareCloudHostKey, new TextBoxSetting("Host", "https://cloud.one-ware.com", "https://cloud.one-ware.com"));
            _settingsService.RegisterCustom("General", "OneWare Cloud", OneWareAccountEmailKey, _oneWareCloudAccountSetting);

            _windowService.RegisterUiExtension("MainWindow_BottomRightExtension", new UiExtension(x =>
                new CloudIntegrationMainWindowBottomRightExtension()
                {
                    DataContext = _cloudIntegrationViewModel
                }));
        }
    }
}