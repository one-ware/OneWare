using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.TerminalManager.ViewModels;

namespace OneWare.TerminalManager
{
    public class TerminalManagerModuleInitializer
    {
        private readonly IDockService _dockService;
        private readonly IWindowService _windowService;
        private readonly TerminalManagerViewModel _terminalManagerViewModel;

        public TerminalManagerModuleInitializer(
            IDockService dockService,
            IWindowService windowService,
            TerminalManagerViewModel terminalManagerViewModel)
        {
            _dockService = dockService ?? throw new ArgumentNullException(nameof(dockService));
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            _terminalManagerViewModel = terminalManagerViewModel ?? throw new ArgumentNullException(nameof(terminalManagerViewModel));
        }

        public void Initialize()
        {
            _dockService.RegisterLayoutExtension<TerminalManagerViewModel>(DockShowLocation.Bottom);

            _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
                new MenuItemViewModel("Terminal")
                {
                    Header = "Terminal",
                    Command = new RelayCommand(() => _dockService.Show(_terminalManagerViewModel)),
                    IconObservable = Application.Current!.GetResourceObservable(TerminalManagerViewModel.IconKey)
                });
        }
    }
}
