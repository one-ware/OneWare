using Microsoft.Extensions.DependencyInjection;
using OneWare.Copilot.Services;
using OneWare.Copilot.Views;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Copilot;

public class CopilotModule : OneWareModuleBase
{
    public const string CopilotCliSettingKey = "AI_Chat_Copilot_CLI";
    
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<CopilotChatService>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.Resolve<ISettingsService>().RegisterSetting("AI Chat", "Copilot CLI", CopilotCliSettingKey,
            new FilePathSetting("Copilot CLI Path", "copilot", null,
                serviceProvider.Resolve<IPaths>().NativeToolsDirectory, PlatformHelper.ExistsOnPath, PlatformHelper.ExeFile)
            {
                HoverDescription = "Path for Copilot CLI"
            });
        
        serviceProvider.Resolve<IChatManagerService>().RegisterChatService(serviceProvider.Resolve<CopilotChatService>());
        serviceProvider.Resolve<IWindowService>().RegisterUiExtension("ChatBot_BottomExtensions", new OneWareUiExtension(x =>
        {
            if(x is CopilotChatService)
                return new CopilotChatBotExtensionView()
                {
                    DataContext = serviceProvider.GetService<CopilotChatService>()
                };
            return null;
        }));
    }
}