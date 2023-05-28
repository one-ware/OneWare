using OneWare.ErrorList.ViewModels;
using Prism.Ioc;
using Prism.Modularity;
using OneWare.Shared.Enums;
using OneWare.Shared.Services;

namespace OneWare.ErrorList;

public class ErrorListModule : IModule
{
    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;
    private readonly IDockService _dockService;
    
    public const string KeyErrorListFilterMode = "ErrorList_FilterMode";
    public const string KeyErrorListShowExternalErrors = "ErrorList_ShowExternalErrors";
    public const string KeyErrorListVisibleSource = "ErrorList_VisibleSource";

    public ErrorListModule(ISettingsService settingsService, IWindowService windowService, IDockService dockService)
    {
        _settingsService = settingsService;
        _windowService = windowService;
        _dockService = dockService;
        
        _dockService.RegisterLayoutExtension<ErrorListViewModel>(DockShowLocation.Bottom);
    }
    
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<ErrorListViewModel>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        _settingsService.Register(KeyErrorListFilterMode, 0);
        _settingsService.Register(KeyErrorListShowExternalErrors, true);
        _settingsService.Register(KeyErrorListVisibleSource, 0);
    }
}