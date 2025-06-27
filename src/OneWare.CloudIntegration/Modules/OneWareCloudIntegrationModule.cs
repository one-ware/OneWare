using System.Runtime.InteropServices;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.Settings;
using OneWare.CloudIntegration.ViewModels;
using OneWare.Essentials.Adapters;
using OneWare.Essentials.Services;

namespace OneWare.CloudIntegration.Modules
{
    public class OneWareCloudIntegrationModule(IContainerAdapter containerAdapter)
    {
        private readonly IContainerAdapter _containerAdapter = containerAdapter;

        public const string Host = "http://localhost:5140";
        public const string OneWareAccountEmailKey = "General_OneWareCloud_AccountEmail";
        public const string CredentialStore = "https://cloud.one-ware.com";


        public void Load()
        {
            _containerAdapter.Register<OneWareCloudAccountSettingViewModel, OneWareCloudAccountSettingViewModel>(isSingleton:true);
            _containerAdapter.Register<OneWareCloudLoginService, OneWareCloudLoginService > (isSingleton: true);
            _containerAdapter.Register<OneWareCloudNotificationService, OneWareCloudNotificationService>(isSingleton: true);

            Register();
        }

        private void Register()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Environment.SetEnvironmentVariable("GCM_CREDENTIAL_STORE", "secretservice");

            _containerAdapter.Resolve<ISettingsService>().RegisterCustom("General", "OneWare Cloud", OneWareAccountEmailKey, new OneWareCloudAccountSetting());
        }
    }
}