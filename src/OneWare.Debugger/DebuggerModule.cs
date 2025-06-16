using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.ViewModels;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Modularity;

namespace OneWare.Debugger;

public class DebuggerModule 
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var dockService = containerProvider.Resolve<IDockService>();
        //dockService.RegisterLayoutExtension<DebuggerViewModel>(DockShowLocation.Bottom);

        containerProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemViewModel("Debugger")
            {
                Header = "Debugger",
                Command = new RelayCommand(() =>
                    dockService.Show(containerProvider.Resolve<DebuggerViewModel>(), DockShowLocation.Bottom)),
                IconObservable = Application.Current!.GetResourceObservable(DebuggerViewModel.IconKey)
            });
    }
}