using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Adapters;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Interfaces;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.TerminalManager.ViewModels;

namespace OneWare.TerminalManager.Modules
{
    public class TerminalManagerModule : IOneWareModule
    {
        private readonly IContainerAdapter _containerAdapter;

        public TerminalManagerModule(IContainerAdapter containerAdapter)
        {
            _containerAdapter = containerAdapter;
        }
        public void RegisterTypes()
        {

            _containerAdapter.Register<TerminalManagerViewModel, TerminalManagerViewModel>(isSingleton:true);
            OnExecute();
        }

        public void OnExecute()
        {
            _containerAdapter.Resolve<IDockService>()
           .RegisterLayoutExtension<TerminalManagerViewModel>(DockShowLocation.Bottom);
            _containerAdapter.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
                new MenuItemViewModel("Terminal")
                {
                    Header = "Terminal",
                    Command = new RelayCommand(() =>
                        _containerAdapter.Resolve<IDockService>()
                            .Show(_containerAdapter.Resolve<TerminalManagerViewModel>())),
                    IconObservable = Application.Current!.GetResourceObservable(TerminalManagerViewModel.IconKey)
                });
        }
    }
}