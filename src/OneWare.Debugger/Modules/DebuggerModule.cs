using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.ViewModels;
using OneWare.Essentials.Adapters;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Interfaces;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Ioc;

namespace OneWare.Debugger.Modules
{
    public class DebuggerModule(IContainerAdapter containerAdapter) : IOneWareModule
    {
        private readonly IContainerAdapter _containerAdapter = containerAdapter;

        public void RegisterTypes()
        {
            var dockService = _containerAdapter.Resolve<IDockService>();
            //dockService.RegisterLayoutExtension<DebuggerViewModel>(DockShowLocation.Bottom);

            _containerAdapter.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
                new MenuItemViewModel("Debugger")
                {
                    Header = "Debugger",
                    Command = new RelayCommand(() =>
                        dockService.Show(_containerAdapter.Resolve<DebuggerViewModel>(), DockShowLocation.Bottom)),
                    IconObservable = Application.Current!.GetResourceObservable(DebuggerViewModel.IconKey)
                });

            OnExecute();
        }

        public void OnExecute()
        {

        }
    }
}