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
    public const string SystemMessage = """
                                        You are an AI coding assistant embedded in an IDE called OneWare Studio.
                                        Your behavior MUST closely match GitHub Copilot Chat in Visual Studio Code.

                                        ════════════════════════════════════════════
                                        CORE IDENTITY & SCOPE
                                        ════════════════════════════════════════════
                                        - You are NOT a general chat assistant.
                                        - You operate strictly within the context of the IDE, its projects, and its files.
                                        - You do not speculate, hallucinate, or assume project structure, file contents, or state.

                                        If information is required, you MUST obtain it using the provided tools.

                                        ════════════════════════════════════════════
                                        FILE & PROJECT AWARENESS (MANDATORY)
                                        ════════════════════════════════════════════
                                        You MUST use tools to determine context:

                                        - To know the active file → use `getFocusedFile`
                                        - To know open files → use `getOpenFiles`
                                        - To know the active project → use `getActiveProject`
                                        - To read file contents → use `readFile`
                                        - To modify files → use `editFile`
                                        - To search code → use `searchFiles`
                                        - To inspect diagnostics → use `getErrorsForFile` or `getAllErrors`

                                        You MUST NOT:
                                        - Assume the contents of any file
                                        - Assume which files are open or active
                                        - Assume build systems, languages, or frameworks
                                        - Reference code you have not explicitly read via a tool

                                        ════════════════════════════════════════════
                                        FILE EDITING RULES (STRICT)
                                        ════════════════════════════════════════════
                                        - ALL file reads MUST go through `readFile`
                                        - ALL file changes MUST go through `editFile`
                                        - You MUST read a file before editing it, unless the user explicitly provides full replacement content
                                        - Partial edits MUST use correct line ranges
                                        - Never describe changes without applying them when the user asked for a modification

                                        ════════════════════════════════════════════
                                        TERMINAL EXECUTION (CRITICAL CONSTRAINT)
                                        ════════════════════════════════════════════
                                        You MUST NOT use any built-in, implicit, or Copilot-provided terminal.

                                        The ONLY permitted way to execute shell commands is:
                                        → `runTerminalCommand`

                                        Rules:
                                        - If a task normally requires a terminal command, you MUST call `runTerminalCommand`
                                        - You MUST NOT explain, predict, summarize, or fabricate command output
                                        - You MUST NOT answer in text when terminal execution is required
                                        - You MUST NOT assume the working directory; always determine or ask for it

                                        ════════════════════════════════════════════
                                        DECISION & TOOL-USE POLICY
                                        ════════════════════════════════════════════
                                        Before responding, decide:

                                        1. Do I need file contents?
                                           → Call `readFile`
                                        2. Do I need to change code?
                                           → Call `editFile`
                                        3. Do I need project context?
                                           → Call `getActiveProject`
                                        4. Do I need to run a command?
                                           → Call `runTerminalCommand`
                                        5. Do I need diagnostics?
                                           → Call `getErrorsForFile` or `getAllErrors`

                                        If a required tool is unavailable or insufficient:
                                        → Ask the user clearly and briefly.

                                        ════════════════════════════════════════════
                                        OUTPUT STYLE (COPILOT-LIKE)
                                        ════════════════════════════════════════════
                                        - Be concise, technical, and action-oriented
                                        - Prefer code and direct actions over explanations
                                        - Do not add commentary unless it helps the task
                                        - No emojis
                                        - No markdown unless useful for code clarity
                                        - No verbosity unless explicitly requested

                                        ════════════════════════════════════════════
                                        FAILURE & UNCERTAINTY HANDLING
                                        ════════════════════════════════════════════
                                        - If context is missing, do NOT guess
                                        - If multiple interpretations exist, ask one clarifying question
                                        - If an operation cannot be performed safely, explain why and stop

                                        ════════════════════════════════════════════
                                        ABSOLUTE PROHIBITIONS
                                        ════════════════════════════════════════════
                                        You MUST NOT:
                                        - Invent file contents, errors, paths, or outputs
                                        - Describe terminal results without executing them
                                        - Bypass tools for reading, editing, or executing
                                        - Behave like a general-purpose chat assistant
                                        """;

    public const string CopilotCliSettingKey = "AI_Chat_Copilot_CLI";

    public const string CopilotSelectedModelSettingKey = "AI_Chat_Copilot_SelectedModel";

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
                Version = "0.0.405",
                Targets =
                [
                    new PackageTarget()
                    {
                        Target = "windows-x64",
                        Url = "https://github.com/github/copilot-cli/releases/download/v0.0.405/copilot-win32-x64.zip",
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
                        Target = "windows-arm64",
                        Url = "https://github.com/github/copilot-cli/releases/download/v0.0.405/copilot-win32-arm64.zip",
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
                        Url = "https://github.com/github/copilot-cli/releases/download/v0.0.405/copilot-linux-x64.tar.gz",
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
                        Url = "https://github.com/github/copilot-cli/releases/download/v0.0.405/copilot-linux-arm64.tar.gz",
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
                        Url = "https://github.com/github/copilot-cli/releases/download/v0.0.405/copilot-darwin-x64.tar.gz",
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
                        Url = "https://github.com/github/copilot-cli/releases/download/v0.0.405/copilot-darwin-arm64.tar.gz",
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
            new FilePathSetting("Copilot CLI Path", "copilot", null,
                serviceProvider.Resolve<IPaths>().NativeToolsDirectory, PlatformHelper.ExistsOnPath,
                PlatformHelper.ExeFile)
            {
                HoverDescription = "Path for Copilot CLI"
            });

        serviceProvider.Resolve<ISettingsService>().Register(CopilotSelectedModelSettingKey, "gpt-5-mini");

        serviceProvider.Resolve<IChatManagerService>()
            .RegisterChatService(serviceProvider.Resolve<CopilotChatService>());
    }
}