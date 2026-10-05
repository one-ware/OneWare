using OneWare.Essentials.Services;

namespace OneWare.Copilot.Services;

/// <summary>GitHub Copilot, or a user-configured BYOK provider.</summary>
public sealed class CopilotChatService(
    ISettingsService settingsService,
    IAiFunctionProvider toolProvider,
    IWindowService windowService,
    IMainDockService mainDockService,
    IPaths paths,
    IChatAgentService agentService)
    : CopilotChatServiceBase(
        settingsService,
        toolProvider,
        windowService,
        mainDockService,
        paths,
        agentService)
{
    public override string Name => "GitHub Copilot";
}
