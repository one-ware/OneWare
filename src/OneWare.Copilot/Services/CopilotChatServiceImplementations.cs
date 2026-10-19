using OneWare.Essentials.Services;

namespace OneWare.Copilot.Services;

/// <summary>GitHub Copilot, or a user-configured BYOK provider.</summary>
public sealed class CopilotChatService : CopilotChatServiceBase
{
    private readonly ISettingsService _settingsService;
    private readonly IAiFunctionProvider _toolProvider;
    private readonly IPackageService _packageService;
    private readonly IPackageWindowService _packageWindowService;
    private readonly IWindowService _windowService;
    private readonly IMainDockService _mainDockService;
    private readonly IPaths _paths;
    private readonly IChatAgentService _agentService;

    public CopilotChatService(
        ISettingsService settingsService,
        IAiFunctionProvider toolProvider,
        IPackageService packageService,
        IPackageWindowService packageWindowService,
        IWindowService windowService,
        IMainDockService mainDockService,
        IPaths paths,
        IChatAgentService agentService)
        : base(
            settingsService,
            toolProvider,
            packageService,
            packageWindowService,
            windowService,
            mainDockService,
            paths,
            agentService)
    {
        _settingsService = settingsService;
        _toolProvider = toolProvider;
        _packageService = packageService;
        _packageWindowService = packageWindowService;
        _windowService = windowService;
        _mainDockService = mainDockService;
        _paths = paths;
        _agentService = agentService;
    }

    public override string Name => "GitHub Copilot";

    protected override CopilotChatServiceBase CreateSibling() => new CopilotChatService(
        _settingsService,
        _toolProvider,
        _packageService,
        _packageWindowService,
        _windowService,
        _mainDockService,
        _paths,
        _agentService);
}
