using OneWare.PackageManager.ViewModels;
using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using VHDPlus.Settings;
using VHDPlus.Shared;
using VHDPlus.Shared.Services;

namespace OneWare.PackageManager;

public class PackageManagerModule : IModule
{
    ////private readonly ILoggerFacade _logger;
    private readonly IModuleTracker _moduleTracker;
    private readonly IEventAggregator _eventAggregator;
    private readonly SettingsService _settingsService;
    private readonly IPaths _paths;
    
    public PackageManagerModule(IModuleTracker moduleTracker, IEventAggregator eventAggregator, SettingsService settingsService, IPaths paths)
    {
        ////_logger = logger;
        _moduleTracker = moduleTracker;
        _eventAggregator = eventAggregator;
        _settingsService = settingsService;
        _paths = paths;
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<PackageManagerViewModel>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        //_settingsService.RegisterSetting("Misc", "Hidden","Output_Autoscroll", "Output Autoscroll", "Output Autoscroll", true, true);
    }
}