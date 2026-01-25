using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.ChatBot.Services;
using OneWare.ChatBot.ViewModels;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.ChatBot;

public class ChatBotModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IChatService, CopilotChatService>();
        services.AddSingleton<ChatBotViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var dockService = serviceProvider.Resolve<IMainDockService>();
        var windowService = serviceProvider.Resolve<IWindowService>();

        dockService.RegisterLayoutExtension<ChatBotViewModel>(DockShowLocation.Bottom);

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("ChatBot")
        {
            Header = "OneAI Chat",
            Command = new RelayCommand(() => dockService.Show(serviceProvider.Resolve<ChatBotViewModel>())),
            IconObservable = Application.Current!.GetResourceObservable(ChatBotViewModel.IconKey),
        });
    }
}
