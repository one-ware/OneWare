using OneWare.Copilot.Connectors;
using OneWare.Copilot.ViewModels;
using OneWare.Essentials.Models;

namespace OneWare.Copilot.Settings;

/// <summary>Settings entry for the connectors; they are stored in their own mcp.json, not in the settings.</summary>
public sealed class CopilotConnectorsSetting : CustomSetting
{
    public CopilotConnectorsSetting(McpConnectorHub hub) : base(string.Empty)
    {
        Control = new CopilotConnectorsSettingViewModel(hub);
    }
}
