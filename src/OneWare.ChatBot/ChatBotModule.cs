using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.ChatBot.ViewModels;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Core.Adapters;
using Prism.Modularity;

namespace OneWare.ChatBot;

public class ChatBotModule : IModule
{
    public void RegisterTypes(IContainerAdapter containerAdapter)
    {
        containerAdapter.Register<ChatBotViewModel, ChatBotViewModel>(isSingleton: true);
    }

    public void OnInitialized(IContainerAdapter containerAdapter)
    {
        var dockService = containerAdapter.Resolve<IDockService>();
        var windowService = containerAdapter.Resolve<IWindowService>();
        
        dockService.RegisterLayoutExtension<ChatBotViewModel>(DockShowLocation.Bottom);
        
        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("ChatBot")
        {
            Header = "OneAI Chat",
            Command = new RelayCommand(() => dockService.Show(containerAdapter.Resolve<ChatBotViewModel>())),
            IconObservable = Application.Current!.GetResourceObservable(ChatBotViewModel.IconKey) ,
        });
    }
}