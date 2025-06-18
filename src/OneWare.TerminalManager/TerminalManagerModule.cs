using Autofac;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.TerminalManager.ViewModels;

namespace OneWare.TerminalManager
{
    public class TerminalManagerModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Register types with Autofac
            builder.RegisterType<TerminalManagerViewModel>().SingleInstance();

            base.Load(builder);
        }

        public void OnInitialized(IComponentContext context)
        {
            var dockService = context.Resolve<IDockService>();
            var windowService = context.Resolve<IWindowService>();

            dockService.RegisterLayoutExtension<TerminalManagerViewModel>(DockShowLocation.Bottom);

            windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
                new MenuItemViewModel("Terminal")
                {
                    Header = "Terminal",
                    Command = new RelayCommand(() =>
                        dockService.Show(context.Resolve<TerminalManagerViewModel>())),
                    IconObservable = Application.Current!.GetResourceObservable(TerminalManagerViewModel.IconKey)
                });
        }
    }
}
