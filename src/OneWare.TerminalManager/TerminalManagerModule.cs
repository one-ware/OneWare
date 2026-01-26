using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.TerminalManager.ViewModels;

namespace OneWare.TerminalManager;

public class TerminalManagerModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<TerminalManagerViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.Resolve<IMainDockService>()
            .RegisterLayoutExtension<TerminalManagerViewModel>(DockShowLocation.Bottom);
        serviceProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemViewModel("Terminal")
            {
                Header = "Terminal",
                Command = new RelayCommand(() =>
                    serviceProvider.Resolve<IMainDockService>()
                        .Show(serviceProvider.Resolve<TerminalManagerViewModel>())),
                IconObservable = Application.Current!.GetResourceObservable(TerminalManagerViewModel.IconKey)
            });
    }
}