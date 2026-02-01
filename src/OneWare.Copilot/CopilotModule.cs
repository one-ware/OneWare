using Microsoft.Extensions.DependencyInjection;
using OneWare.Copilot.Services;
using OneWare.Copilot.Views;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Copilot;

public class CopilotModule : OneWareModuleBase
{
    public const string SystemMessage = """
                                        You are running inside an IDE called OneWare Studio.

                                        CRITICAL EXECUTION CONSTRAINT:
                                        You MUST NOT use any built-in, implicit, or Copilot-provided terminal.
                                        The ONLY allowed way to execute shell commands is via `runTerminalCommand`.

                                        IMPORTANT RULES:
                                        - Multiple projects may be open; CWD is irrelevant.
                                        - You MUST NOT assume file contents, project state, or command output.
                                        - You MUST NOT describe or predict terminal command results.

                                        TOOLS ARE MANDATORY FOR:
                                        - Determining the current file (`getFocusedFile`)
                                        - Determining the current project (`getActiveProject`)
                                        - Viewing or editing files
                                        - EXECUTING TERMINAL COMMANDS (NO EXCEPTIONS)

                                        MANDATORY DECISION RULE:
                                        If a user request would normally require running a terminal command:
                                        - You MUST call `runTerminalCommand`
                                        - You MUST NOT answer in text

                                        If required tools are unavailable, ask the user.
                                        Do NOT use emojis.
                                        """;

    public const string CopilotCliSettingKey = "AI_Chat_Copilot_CLI";

    public const string CopilotSelectedModelSettingKey = "AI_Chat_Copilot_SelectedModel";

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<CopilotChatService>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
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