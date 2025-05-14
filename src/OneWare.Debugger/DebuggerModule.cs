using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.ViewModels;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Autofac;

namespace OneWare.Debugger
{
    public class DebuggerModule
    {
        public void RegisterTypes(ContainerBuilder builder)
        {
            // Register types with Autofac container
            // For example:
            // builder.RegisterType<DebuggerViewModel>().AsSelf();
        }

        public void OnInitialized(IContainer container)
        {
            // Resolve dependencies using Autofac
            var dockService = container.Resolve<IDockService>();
            var windowService = container.Resolve<IWindowService>();

            windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
                new MenuItemViewModel("Debugger")
                {
                    Header = "Debugger",
                    Command = new RelayCommand(() =>
                        dockService.Show(container.Resolve<DebuggerViewModel>(), DockShowLocation.Bottom)),
                    IconObservable = Application.Current!.GetResourceObservable(DebuggerViewModel.IconKey)
                });
        }
    }
}
