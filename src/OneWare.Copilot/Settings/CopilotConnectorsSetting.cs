using OneWare.Copilot.Connectors;
using OneWare.Copilot.ViewModels;
using OneWare.Essentials.Models;

namespace OneWare.Copilot.Settings;

/// <summary>Settings entry for the connectors; they are stored in the Copilot CLI configuration, not here.</summary>
public sealed class CopilotConnectorsSetting : CustomSetting
{
    public CopilotConnectorsSetting(CopilotConnectorService service) : base(string.Empty)
    {
        Control = new CopilotConnectorsSettingViewModel(service);
    }
}
