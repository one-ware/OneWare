using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.ViewModels;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using System; // For ArgumentNullException

namespace OneWare.Debugger;

public class DebuggerModuleInitializer
{
    private readonly IDockService _dockService;
    private readonly IWindowService _windowService;
    private readonly IDebuggerViewModelFactory _debuggerViewModelFactory; // New injected dependency

    // Constructor injection for all required services
    public DebuggerModuleInitializer(
        IDockService dockService,
        IWindowService windowService,
        IDebuggerViewModelFactory debuggerViewModelFactory) // Inject the factory
    {
        _dockService = dockService ?? throw new ArgumentNullException(nameof(dockService));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        _debuggerViewModelFactory = debuggerViewModelFactory ?? throw new ArgumentNullException(nameof(debuggerViewModelFactory));
    }

    // This method will contain the original initialization logic
    public void Initialize()
    {
        _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemViewModel("Debugger")
            {
                Header = "Debugger",
                Command = new RelayCommand(() =>
                {
                    // Use the injected factory to create the DebuggerViewModel
                    var debuggerViewModel = _debuggerViewModelFactory.Create();
                    _dockService.Show(debuggerViewModel, DockShowLocation.Bottom);
                }),
                IconObservable = Application.Current!.GetResourceObservable(DebuggerViewModel.IconKey)
            });
    }
}