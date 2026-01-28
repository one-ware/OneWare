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
        services.AddSingleton<AiFunctionProvider>();
        services.AddSingleton<IAiFunctionProvider>(provider => provider.Resolve<AiFunctionProvider>());
        services.AddSingleton<ChatBotViewModel>();
        services.AddSingleton<IChatManagerService>(provider => provider.Resolve<ChatBotViewModel>());
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var dockService = serviceProvider.Resolve<IMainDockService>();
        var windowService = serviceProvider.Resolve<IWindowService>();

        dockService.RegisterLayoutExtension<IChatManagerService>(DockShowLocation.Right);

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("Chat")
        {
            Header = "Chat",
            Command = new RelayCommand(() => dockService.Show(serviceProvider.Resolve<IChatManagerService>())),
            IconObservable = Application.Current!.GetResourceObservable(ChatBotViewModel.IconKey),
        });
    }
}
