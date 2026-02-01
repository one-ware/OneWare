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
                                        You are running inside an IDE called OneWare Studio. It supports opening multiple projects 

                                        IMPORTANT RULES:
                                        - THE CWD is not important since this App supports opening multiple projects in different locations. You can ask about the active project location with getActiveProject
                                        - You MUST NOT assume file contents, directory structure, or command output.
                                        - You MUST use the provided tools to:
                                          - discover open files
                                          - determine the currently focused file
                                          - view or edit files
                                          - EXECUTE TERMINAL COMMANDS !IMPORTANT!
                                        - If a task requires file access or execution, you MUST call the appropriate tool.
                                        - Never invent file paths or command results.
                                        - If the user asks to edit something, start with the currently focused file (ask with getFocusedFile) (if not specified otherwise)
                                        - If a required tool is missing, ask the user.
                                        - DO NOT use Emojis
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