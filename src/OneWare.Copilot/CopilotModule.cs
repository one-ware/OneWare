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
    public const string CopilotCliSettingKey = "AI_Chat_Copilot_CLI";
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

    public static readonly Package CopilotPackage = new()
    {
        Category = "Binaries",
        Id = "copilotcli",
        Type = "NativeTool",
        Name = "Copilot CLI",
        Description = "Used for Copilot Integration",
        License = "GitHub Copilot CLI License",
        IconUrl = "https://github.githubassets.com/images/modules/site/copilot/copilot.png",
        AcceptLicenseBeforeDownload = true,
        Links =
        [
            new PackageLink
            {
                Name = "GitHub",
                Url = "https://github.com/github/copilot-cli"
            },
            new PackageLink()
            {
                Name = "Documentation",
                Url = "https://docs.github.com/en/copilot/concepts/agents/about-copilot-cli"
            }
        ],
        Tabs =
        [
            new PackageTab
            {
                Title = "License",
                ContentUrl = "https://raw.githubusercontent.com/github/copilot-cli/refs/heads/main/LICENSE.md"
            },
            new PackageTab
            {
                Title = "Changelog",
                ContentUrl = "https://raw.githubusercontent.com/github/copilot-cli/refs/heads/main/changelog.md"
            }
        ],
        Versions =
        [
            new PackageVersion()
            {
                Version = "1.0.83",
                Targets =
                [
                    new PackageTarget()
                    {
                        Target = "win-x64",
                        Url = "https://github.com/github/copilot-cli/releases/download/v1.0.83/copilot-win32-x64.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "copilot.exe",
                                SettingKey = CopilotCliSettingKey
                            }
                        ]
                    },
                    new PackageTarget()
                    {
                        Target = "win-arm64",
                        Url = "https://github.com/github/copilot-cli/releases/download/v1.0.83/copilot-win32-arm64.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "copilot.exe",
                                SettingKey = CopilotCliSettingKey
                            }
                        ]
                    },
                    new PackageTarget()
                    {
                        Target = "linux-x64",
                        Url = "https://github.com/github/copilot-cli/releases/download/v1.0.83/copilot-linux-x64.tar.gz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "copilot",
                                SettingKey = CopilotCliSettingKey
                            }
                        ]
                    },
                    new PackageTarget()
                    {
                        Target = "linux-arm64",
                        Url = "https://github.com/github/copilot-cli/releases/download/v1.0.83/copilot-linux-arm64.tar.gz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "copilot",
                                SettingKey = CopilotCliSettingKey
                            }
                        ]
                    },
                    new PackageTarget()
                    {
                        Target = "osx-x64",
                        Url = "https://github.com/github/copilot-cli/releases/download/v1.0.83/copilot-darwin-x64.tar.gz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "copilot",
                                SettingKey = CopilotCliSettingKey
                            }
                        ]
                    },
                    new PackageTarget()
                    {
                        Target = "osx-arm64",
                        Url = "https://github.com/github/copilot-cli/releases/download/v1.0.83/copilot-darwin-arm64.tar.gz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "copilot",
                                SettingKey = CopilotCliSettingKey
                            }
                        ]
                    },
                ]
            }
        ]
    };

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
        serviceProvider.Resolve<IPackageService>().RegisterPackage(CopilotPackage);

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

        settingsService.RegisterSetting("AI Chat", "Copilot CLI", CopilotCliSettingKey,
            new FilePathSetting("Copilot CLI Path", "", null,
                serviceProvider.Resolve<IPaths>().NativeToolsDirectory, PlatformHelper.ExistsOnPath,
                PlatformHelper.ExeFile)
            {
                HoverDescription = "Path for Copilot CLI"
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
}
