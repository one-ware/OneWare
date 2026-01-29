using Microsoft.Extensions.DependencyInjection;
using OneWare.Copilot.Services;
using OneWare.Copilot.Views;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Copilot;

public class CopilotModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<CopilotChatService>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
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