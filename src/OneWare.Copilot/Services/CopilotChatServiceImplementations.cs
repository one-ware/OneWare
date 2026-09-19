using OneWare.Essentials.Services;

namespace OneWare.Copilot.Services;

public sealed class CopilotChatService(
    ISettingsService settingsService,
    IAiFunctionProvider toolProvider,
    IPackageService packageService,
    IPackageWindowService packageWindowService,
    IWindowService windowService,
    IMainDockService mainDockService,
    IPaths paths,
    IChatAgentService agentService)
    : CopilotChatServiceBase(
        settingsService,
        toolProvider,
        packageService,
        packageWindowService,
        windowService,
        mainDockService,
        paths,
        agentService,
        null,
        false);

public sealed class OneWareCloudChatService(
    ISettingsService settingsService,
    IAiFunctionProvider toolProvider,
    IPackageService packageService,
    IPackageWindowService packageWindowService,
    IWindowService windowService,
    IMainDockService mainDockService,
    IPaths paths,
    IChatAgentService agentService,
    IOneWareCloudAccess cloudAccess)
    : CopilotChatServiceBase(
        settingsService,
        toolProvider,
        packageService,
        packageWindowService,
        windowService,
        mainDockService,
        paths,
        agentService,
        cloudAccess,
        true);
