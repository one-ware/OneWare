using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.ViewModels; // Ensure DebuggerViewModel is defined
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services; // Ensure IDockService, IWindowService are defined
using OneWare.Essentials.ViewModels;
using System; // For ArgumentNullException

namespace OneWare.Debugger;

// New service to encapsulate the initialization logic
public class DebuggerModuleInitializer
{
    private readonly IDockService _dockService;
    private readonly IWindowService _windowService;
    private readonly DebuggerViewModel _debuggerViewModel;

    // Constructor injection for all required services
    public DebuggerModuleInitializer(IDockService dockService,
                                    DebuggerViewModel debuggerViewModel,
                                    IWindowService windowService)
    {
        _dockService = dockService ?? throw new ArgumentNullException(nameof(dockService));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        _debuggerViewModel = debuggerViewModel ?? throw new ArgumentNullException(nameof(debuggerViewModel));
    }

    // This method will contain the original initialization logic
    public void Initialize()
    {
        // Removed the commented out line if it's not active.
        // If you need to register a layout extension for DebuggerViewModel,
        // consider if DebuggerViewModel should be directly resolved here,
        // or if it should be lazy-loaded, or if a factory should be injected.
        // For simplicity, I'll keep the current behavior of resolving DebuggerViewModel
        // within the command, but acknowledge it's still a "lazy" service locator.
        // A factory for DebuggerViewModel could be injected here if creating new instances frequently.

        _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemViewModel("Debugger")
            {
                Header = "Debugger",
                // This RelayCommand still resolves DebuggerViewModel from the ContainerLocator.
                // For a true DI approach, you might want to inject a Func<DebuggerViewModel>
                // or a IDebuggerViewModelFactory into this initializer if DebuggerViewModel is complex.
                // However, for a simple view model resolved on demand, this might be a pragmatic choice.
                // If DebuggerViewModel itself has dependencies, the container will resolve them when it's created.
                Command = new RelayCommand(() =>
                {
                    // This is still a service locator usage for DebuggerViewModel.
                    // For a completely pure approach, you'd inject a factory for DebuggerViewModel.
                    // However, given this is inside a command, it's often a pragmatic choice
                    // for lazy instantiation.                    
                    _dockService.Show(_debuggerViewModel, DockShowLocation.Bottom);
                }),
                IconObservable = Application.Current!.GetResourceObservable(DebuggerViewModel.IconKey)
            });
    }
}