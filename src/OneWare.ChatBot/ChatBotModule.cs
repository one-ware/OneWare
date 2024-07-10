using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.ChatBot.ViewModels;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.ChatBot;

public class ChatBotModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<ChatBotViewModel>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var dockService = containerProvider.Resolve<IDockService>();
        var windowService = containerProvider.Resolve<IWindowService>();
        
        dockService.RegisterLayoutExtension<ChatBotViewModel>(DockShowLocation.Bottom);
        
        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("ChatBot")
        {
            Header = "OneAI Chat",
            Command = new RelayCommand(() => dockService.Show(containerProvider.Resolve<ChatBotViewModel>())),
            IconObservable = Application.Current!.GetResourceObservable(ChatBotViewModel.IconKey) ,
        });
    }
}