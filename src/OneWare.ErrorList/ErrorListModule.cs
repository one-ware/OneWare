using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.ErrorList.ViewModels;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.ErrorList;

public class ErrorListModule : IModule
{
    public const string KeyErrorListFilterMode = "ErrorList_FilterMode";
    public const string KeyErrorListShowExternalErrors = "ErrorList_ShowExternalErrors";
    public const string KeyErrorListVisibleSource = "ErrorList_VisibleSource";
    private readonly IDockService _dockService;
    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;

    public ErrorListModule(ISettingsService settingsService, IWindowService windowService, IDockService dockService)
    {
        _settingsService = settingsService;
        _windowService = windowService;
        _dockService = dockService;
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterManySingleton<ErrorListViewModel>(typeof(IErrorService),
            typeof(ErrorListViewModel));
    
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        _dockService.RegisterLayoutExtension<IErrorService>(DockShowLocation.Bottom);

        _settingsService.Register(KeyErrorListFilterMode, 0);
        _settingsService.RegisterTitled("Experimental", "Errors", KeyErrorListShowExternalErrors,
            "Show external errors", "Sets if errors from files outside of your project should be visible", false);
        _settingsService.Register(KeyErrorListVisibleSource, 0);

        _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("Problems")
        {
            Header = "Problems",
            Command = new RelayCommand(() => _dockService.Show(containerProvider.Resolve<IErrorService>())),
            IconObservable = Application.Current!.GetResourceObservable(ErrorListViewModel.IconKey)
        });
    }
}