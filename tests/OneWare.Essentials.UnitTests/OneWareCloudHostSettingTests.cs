using OneWare.CloudIntegration;
using OneWare.Essentials.Models;
using Xunit;

namespace OneWare.Essentials.UnitTests;

public class OneWareCloudHostSettingTests
{
    [Fact]
    public void CloudHostSetting_DoesNotWarn_WhenDefaultHostIsUsed()
    {
        var setting = new TextBoxSetting("Host", OneWareCloudIntegrationModule.CredentialStore,
            OneWareCloudIntegrationModule.CredentialStore)
        {
            Validator = OneWareCloudIntegrationModule.CloudHostValidation
        };

        Assert.Null(setting.ValidationMessage);
        Assert.Equal(OneWareCloudIntegrationModule.CredentialStore,
            OneWareCloudIntegrationModule.GetCloudHost(setting.Value.ToString()));
    }

    [Fact]
    public void CloudHostSetting_WarnsAndFallsBack_WhenHostIsCleared()
    {
        var setting = new TextBoxSetting("Host", OneWareCloudIntegrationModule.CredentialStore,
            OneWareCloudIntegrationModule.CredentialStore)
        {
            Validator = OneWareCloudIntegrationModule.CloudHostValidation
        };

        setting.Value = string.Empty;

        Assert.Equal($"\u26A0 Host is empty. Using {OneWareCloudIntegrationModule.CredentialStore}/.",
            setting.ValidationMessage);
        Assert.Equal(OneWareCloudIntegrationModule.CredentialStore,
            OneWareCloudIntegrationModule.GetCloudHost(setting.Value.ToString()));
    }
}
