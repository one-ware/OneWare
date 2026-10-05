using System.Reactive.Linq;
using Microsoft.Extensions.DependencyInjection;
using OneWare.CloudIntegration;
using OneWare.Copilot.Services;
using OneWare.Copilot.Views;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.Copilot;

public class CopilotModule : OneWareModuleBase
{
    public const string CopilotSelectedModelSettingKey = "AI_Chat_Copilot_SelectedModel";
    public const string CopilotSelectedReasoningEffortSettingKey = "AI_Chat_Copilot_SelectedReasoningEffort";
    public const string CopilotApprovalModeSettingKey = "AI_Chat_Copilot_ApprovalMode";
    public const string CopilotContextTierSettingKey = "AI_Chat_Copilot_ContextTier";
    public const string CopilotAutoTierSettingKey = "AI_Chat_Copilot_AutoTier";
    public const string CopilotProviderSettingKey = "AI_Chat_Copilot_Provider";
    public const string CopilotByokEndpointSettingKey = "AI_Chat_Copilot_BYOK_Endpoint";
    public const string CopilotByokApiKeyEnvironmentVariableSettingKey =
        "AI_Chat_Copilot_BYOK_ApiKeyEnvironmentVariable";
    public const string CopilotByokModelSettingKey = "AI_Chat_Copilot_BYOK_Model";
    public const string CopilotByokWireApiSettingKey = "AI_Chat_Copilot_BYOK_WireApi";
    public const string CopilotByokSelectedModelSettingKey = "AI_Chat_Copilot_BYOK_SelectedModel";
    public const string CopilotOneWareCloudSelectedModelSettingKey =
        "AI_Chat_Copilot_OneWareCloud_SelectedModel";

    public const string ProviderGitHubCopilot = "GitHub Copilot";
    public const string ProviderOneWareCloud = "OneWare Cloud";
    public const string ProviderOpenAiCompatible = "OpenAI Compatible (BYOK)";
    public const string ProviderAnthropic = "Anthropic (BYOK)";
    public const string WireApiCompletions = "completions";
    public const string WireApiResponses = "responses";

    /// <summary>
    /// Default model, matching the Copilot CLI (and the VS Code Agent Host built on it).
    /// </summary>
    public const string DefaultModelId = "claude-sonnet-4-5";

    /// <summary>
    /// Id of the model that lets Copilot route each turn to a backend model itself. Sessions using
    /// it can express a routing preference through <see cref="CopilotAutoTierSettingKey"/>.
    /// </summary>
    public const string AutoModelId = "auto";

    /// <summary>
    /// Default reasoning effort, matching the Copilot CLI <c>effortLevel</c> default.
    /// </summary>
    public const string DefaultReasoningEffort = "medium";

    /// <summary>
    /// Id of the package that installed the Copilot CLI before the runtime was bundled with OneWare.
    /// </summary>
    private const string LegacyCopilotCliPackageId = "copilotcli";

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<CopilotChatService>();
        services.AddTransient<OneWareCloudChatService>();
    }

    public override IReadOnlyCollection<string> Dependencies =>
    [
        nameof(OneWareCloudIntegrationModule),
    ];

    public override void Initialize(IServiceProvider serviceProvider)
    {
        RemoveLegacyCopilotCliPackage(serviceProvider.Resolve<IPackageService>());

        var settingsService = serviceProvider.Resolve<ISettingsService>();

        settingsService.RegisterSetting("AI Chat", "Copilot CLI", CopilotProviderSettingKey,
            new ComboBoxSetting("Model Provider", ProviderGitHubCopilot,
                [ProviderGitHubCopilot, ProviderOpenAiCompatible, ProviderAnthropic])
            {
                HoverDescription =
                    "GitHub Copilot uses your Copilot account. BYOK connects directly to the configured provider. " +
                    "OneWare Cloud is available as a separate chat service."
            });
        if (settingsService.GetSettingValue<string>(CopilotProviderSettingKey) == ProviderOneWareCloud)
            settingsService.SetSettingValue(CopilotProviderSettingKey, ProviderGitHubCopilot);

        var byokVisible = settingsService.GetSettingObservable<string>(CopilotProviderSettingKey)
            .Select(provider => provider is ProviderOpenAiCompatible or ProviderAnthropic);

        settingsService.RegisterSetting("AI Chat", "Copilot CLI", CopilotByokEndpointSettingKey,
            new TextBoxSetting("BYOK Endpoint", "", "http://localhost:11434/v1")
            {
                HoverDescription =
                    "Provider API base URL. Leave blank for http://localhost:11434/v1 (OpenAI-compatible) " +
                    "or https://api.anthropic.com (Anthropic).",
                IsVisibleObservable = byokVisible
            });

        settingsService.RegisterSetting("AI Chat", "Copilot CLI",
            CopilotByokApiKeyEnvironmentVariableSettingKey,
            new TextBoxSetting("API Key Environment Variable", "", "OPENAI_API_KEY")
            {
                HoverDescription =
                    "Optional environment variable containing the API key. The key itself is never saved in " +
                    "OneWare settings or Copilot session files.",
                IsVisibleObservable = byokVisible
            });

        settingsService.RegisterSetting("AI Chat", "Copilot CLI", CopilotByokModelSettingKey,
            new TextBoxSetting("Model Override", "", "qwen2.5-coder:7b")
            {
                HoverDescription =
                    "Optional model ID. Leave blank to discover models from the provider's /models endpoint.",
                IsVisibleObservable = byokVisible
            });

        settingsService.RegisterSetting("AI Chat", "Copilot CLI", CopilotByokWireApiSettingKey,
            new ComboBoxSetting("OpenAI Wire API", WireApiCompletions,
                [WireApiCompletions, WireApiResponses])
            {
                HoverDescription =
                    "Use completions for Ollama and broad OpenAI compatibility. Use responses for providers " +
                    "that implement the OpenAI Responses API. Ignored for Anthropic.",
                IsVisibleObservable = byokVisible
            });

        settingsService.RegisterSetting("AI Chat", "Copilot CLI",
            CopilotApprovalModeSettingKey,
            new ComboBoxSetting("Approval Mode",
                CopilotChatService.ApprovalModeDefault,
                new object[]
                {
                    CopilotChatService.ApprovalModeDefault,
                    CopilotChatService.ApprovalModeBypass,
                    CopilotChatService.ApprovalModeAutopilot
                })
            {
                HoverDescription =
                    "Default: ask before running tools that need confirmation. " +
                    "Bypass Approval: automatically approve all permission requests without prompting. " +
                    "Autopilot: Bypass Approval plus automatically answer agent questions " +
                    "(the agent is told you are unavailable and decides what is best)."
            });

        settingsService.RegisterSetting("AI Chat", "Copilot CLI",
            CopilotContextTierSettingKey,
            new ComboBoxSetting("Context Length",
                CopilotChatService.ContextTierDefault,
                new object[]
                {
                    CopilotChatService.ContextTierDefault,
                    CopilotChatService.ContextTierLong
                })
            {
                HoverDescription =
                    "Default: use the model's standard context window. " +
                    "Long Context: request an extended context window for models that support it " +
                    "(may increase cost and latency)."
            });

        // serviceProvider.Resolve<ISettingsService>().RegisterSetting("AI Chat", "Copilot CLI",
        //     CopilotRemoteSessionSettingKey,
        //     new CheckBoxSetting("Create Remote Session", false)
        //     {
        //         HoverDescription = "When enabled, new sessions are created as remote sessions (Mission Control). The remote URL is shown in the chat toolbar."
        //     });

        settingsService.RegisterSetting("AI Chat", "Copilot CLI",
            CopilotAutoTierSettingKey,
            new ComboBoxSetting("Auto Routing",
                CopilotChatService.AutoTierDefault,
                new object[]
                {
                    CopilotChatService.AutoTierDefault,
                    CopilotChatService.AutoTierEfficiency,
                    CopilotChatService.AutoTierBalance,
                    CopilotChatService.AutoTierIntelligence
                })
            {
                HoverDescription =
                    "Routing preference for the \"auto\" model, which lets Copilot pick a backend " +
                    "model per turn. Default: leave the choice to Copilot. Efficiency: prefer " +
                    "faster, cheaper models. Balance: trade off speed and capability. " +
                    "Intelligence: prefer the most capable models."
            });

        settingsService.Register(CopilotSelectedModelSettingKey, DefaultModelId);
        settingsService.Register(CopilotByokSelectedModelSettingKey, "");
        settingsService.Register(CopilotOneWareCloudSelectedModelSettingKey, "");

        settingsService.Register(CopilotSelectedReasoningEffortSettingKey, "");

        // The first registered service is the default selection.
        serviceProvider.Resolve<IChatManagerService>()
            .RegisterChatService(serviceProvider.Resolve<OneWareCloudChatService>());
        serviceProvider.Resolve<IChatManagerService>()
            .RegisterChatService(serviceProvider.Resolve<CopilotChatService>());
    }

    /// <summary>
    /// The Copilot runtime ships with OneWare now, so a Copilot CLI installed through the package manager
    /// is no longer used. Removes it once the installed packages are known.
    /// </summary>
    private static void RemoveLegacyCopilotCliPackage(IPackageService packageService)
    {
        var removing = false;

        void TryRemove()
        {
            if (removing ||
                !packageService.Packages.TryGetValue(LegacyCopilotCliPackageId, out var state) ||
                state.InstalledVersion == null)
                return;

            removing = true;
            packageService.PackagesUpdated -= OnPackagesUpdated;
            _ = packageService.RemoveAsync(LegacyCopilotCliPackageId);
        }

        void OnPackagesUpdated(object? sender, EventArgs e) => TryRemove();

        packageService.PackagesUpdated += OnPackagesUpdated;
        TryRemove();
    }
}
