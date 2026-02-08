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
        
        dockService.RegisterLayoutExtension<IChatManagerService>(DockShowLocation.Right);

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("AI Chat")
        {
            Header = "AI Chat",
            Command = new RelayCommand(() => dockService.Show(serviceProvider.Resolve<IChatManagerService>())),
            IconModel = new IconModel(ChatViewModel.IconKey),
        });
    }
}
