using Autofac;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.TerminalManager.ViewModels;

namespace OneWare.TerminalManager;

public class TerminalManagerModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<TerminalManagerViewModel>().SingleInstance();

        builder.RegisterBuildCallback(container =>
        {
            var dockService = container.Resolve<IDockService>();
            var windowService = container.Resolve<IWindowService>();
            var terminalVm = container.Resolve<TerminalManagerViewModel>();

            dockService.RegisterLayoutExtension<TerminalManagerViewModel>(DockShowLocation.Bottom);

            windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("Terminal")
            {
                Header = "Terminal",
                Command = new RelayCommand(() => dockService.Show(terminalVm)),
                IconObservable = Application.Current!.GetResourceObservable(TerminalManagerViewModel.IconKey)
            });
        });
    }
}
