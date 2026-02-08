using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Debugger.ViewModels;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger;

public class DebuggerModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<DebuggerViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var dockService = serviceProvider.Resolve<IMainDockService>();

        serviceProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemViewModel("Debugger")
            {
                Header = "Debugger",
                Command = new RelayCommand(() =>
                    dockService.Show(serviceProvider.Resolve<DebuggerViewModel>(), DockShowLocation.Bottom)),
                IconModel = new IconModel(DebuggerViewModel.IconKey)
            });
    }
}