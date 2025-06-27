using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.ViewModels;
using OneWare.Essentials.Adapters;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Ioc;

namespace OneWare.Debugger.Modules
{
    public class DebuggerModule
    {
        private readonly IContainerAdapter _containerAdapter;
      


        public DebuggerModule(IContainerAdapter containerAdapter)
        {
            _containerAdapter = containerAdapter;
        }

        public void Load()
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

            Register();
        }

        private void Register()
        {

        }
    }
}