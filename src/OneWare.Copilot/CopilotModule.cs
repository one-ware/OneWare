using Microsoft.Extensions.DependencyInjection;
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

    public static readonly Package CopilotPackage = new()
    {
        Category = "Binaries",
        Id = "copilotcli",
        Type = "NativeTool",
        Name = "Copilot CLI",
        Description = "Used for Copilot Integration",
        License = "GitHub Copilot CLI License",
        IconUrl =
            "https://raw.githubusercontent.com/lobehub/lobe-icons/refs/heads/master/packages/static-png/dark/githubcopilot.png",
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
                Version = "1.0.74",
                Targets =
                [
                    new PackageTarget()
                    {
                        Target = "win-x64",
                        Url = "https://github.com/github/copilot-cli/releases/download/v1.0.74/copilot-win32-x64.zip",
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
                        Url = "https://github.com/github/copilot-cli/releases/download/v1.0.74/copilot-win32-arm64.zip",
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
                        Url = "https://github.com/github/copilot-cli/releases/download/v1.0.74/copilot-linux-x64.tar.gz",
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
                        Url = "https://github.com/github/copilot-cli/releases/download/v1.0.74/copilot-linux-arm64.tar.gz",
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
                        Url = "https://github.com/github/copilot-cli/releases/download/v1.0.74/copilot-darwin-x64.tar.gz",
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
                        Url = "https://github.com/github/copilot-cli/releases/download/v1.0.74/copilot-darwin-arm64.tar.gz",
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
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.Resolve<IPackageService>().RegisterPackage(CopilotPackage);
        
        serviceProvider.Resolve<ISettingsService>().RegisterSetting("AI Chat", "Copilot CLI", CopilotCliSettingKey,
            new FilePathSetting("Copilot CLI Path", "", null,
                serviceProvider.Resolve<IPaths>().NativeToolsDirectory, PlatformHelper.ExistsOnPath,
                PlatformHelper.ExeFile)
            {
                HoverDescription = "Path for Copilot CLI"
            });

        serviceProvider.Resolve<ISettingsService>().RegisterSetting("AI Chat", "Copilot CLI",
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

        serviceProvider.Resolve<ISettingsService>().RegisterSetting("AI Chat", "Copilot CLI",
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

        serviceProvider.Resolve<ISettingsService>().Register(CopilotSelectedModelSettingKey, "gpt-5-mini");

        serviceProvider.Resolve<ISettingsService>().Register(CopilotSelectedReasoningEffortSettingKey, "");

        serviceProvider.Resolve<IChatManagerService>()
            .RegisterChatService(serviceProvider.Resolve<CopilotChatService>());
    }
}
