using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Chat.Services;
using OneWare.Chat.ViewModels;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Chat;

public class ChatModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<AiFunctionProvider>();
        services.AddSingleton<IAiFunctionProvider>(provider => provider.Resolve<AiFunctionProvider>());
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<IChatManagerService>(provider => provider.Resolve<ChatViewModel>());

        services.AddSingleton<AiFileEditService>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var dockService = serviceProvider.Resolve<IMainDockService>();
        var windowService = serviceProvider.Resolve<IWindowService>();
        var settingsService = serviceProvider.Resolve<ISettingsService>();
        dockService.RegisterLayoutExtension<IChatManagerService>(DockShowLocation.RightPinned);
        
        settingsService.RegisterSettingCategory("AI Chat", 0, "Bootstrap.ChatLeft");

        settingsService.RegisterSetting("AI Chat", "History", ChatViewModel.MaxSessionHistoryKey,
            new SliderSetting("Maximum stored chats", ChatViewModel.DefaultMaxSessionHistory, 5, 500, 5)
            {
                HoverDescription =
                    "Maximum number of chats kept per AI service. Older chats are deleted automatically."
            });
        
        dockService.RegisterLayoutExtension<IChatManagerService>(DockShowLocation.Right);

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemModel("AI Chat")
        {
            Header = "AI Chat",
            Command = new RelayCommand(() => dockService.Show(serviceProvider.Resolve<IChatManagerService>(), DockShowLocation.RightPinned)),
            Icon = new IconModel(ChatViewModel.IconKey),
        });
    }
}
