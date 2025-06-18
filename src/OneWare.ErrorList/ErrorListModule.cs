using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.ErrorList.ViewModels;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Modularity;
using Prism.Ioc; // Required for IContainerRegistry

namespace OneWare.ErrorList;

public class ErrorListModule : IModule // Explicitly implementing IModule for clarity
{
    public const string KeyErrorListFilterMode = "ErrorList_FilterMode";
    public const string KeyErrorListShowExternalErrors = "ErrorList_ShowExternalErrors";
    public const string KeyErrorListVisibleSource = "ErrorList_VisibleSource";

    private readonly IDockService _dockService;
    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;
    private readonly IErrorService _errorService; // New injected dependency for the menu item command

    // Constructor injection for all required services
    public ErrorListModule(
        ISettingsService settingsService,
        IWindowService windowService,
        IDockService dockService,
        IErrorService errorService) // Inject IErrorService directly
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        _dockService = dockService ?? throw new ArgumentNullException(nameof(dockService));
        _errorService = errorService ?? throw new ArgumentNullException(nameof(errorService)); // Now injecting this
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // This registration correctly maps ErrorListViewModel to both IErrorService and itself as a singleton.
        containerRegistry.RegisterManySingleton<ErrorListViewModel>(typeof(IErrorService),
            typeof(ErrorListViewModel));

        // Any other types specific to the ErrorList module would be registered here.
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        // _dockService and _settingsService are already injected via the constructor, which is good.
        _dockService.RegisterLayoutExtension<IErrorService>(DockShowLocation.Bottom);

        _settingsService.Register(KeyErrorListFilterMode, 0);
        _settingsService.RegisterTitled("Experimental", "Errors", KeyErrorListShowExternalErrors,
            "Show external errors", "Sets if errors from files outside of your project should be visible", false);
        _settingsService.Register(KeyErrorListVisibleSource, 0);

        _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("Problems")
        {
            Header = "Problems",
            Command = new RelayCommand(() =>
            {
                // Now directly use the injected _errorService
                _dockService.Show(_errorService);
            }),
            IconObservable = Application.Current!.GetResourceObservable(ErrorListViewModel.IconKey)
        });
    }
}