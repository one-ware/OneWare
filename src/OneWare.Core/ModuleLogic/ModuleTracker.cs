using System.Globalization;
using OneWare.Shared.Enums;
using OneWare.Shared.Services;
using Prism.Modularity;

namespace OneWare.Core.ModuleLogic;

class ModuleTracker : IModuleTracker
{
    private readonly ILogger _logger;

    public IModuleCatalog ModuleCatalog { get; }
    
    public IModuleManager ModuleManager { get; }

    
    public ModuleTracker(ILogger logger, IModuleCatalog moduleCatalog, IModuleManager moduleManager)
    {
        _logger = logger;
        ModuleCatalog = moduleCatalog;
        ModuleManager = moduleManager;
    }
    
    public void RecordModuleLoaded(string moduleName)
    {
        _logger.Log(string.Format(CultureInfo.CurrentCulture, "'{0}' module loaded.", moduleName));
    }

    public void RecordModuleConstructed(string moduleName)
    {
        var moduleTrackingState = GetModuleTrackingState(moduleName);
        if (moduleTrackingState != null)
        {
            moduleTrackingState.ModuleInitializationStatus = ModuleInitializationStatus.Constructed;
        }

        _logger.Log(string.Format(CultureInfo.CurrentCulture, "'{0}' module constructed.", moduleName));
    }

    public void RecordModuleInitialized(string moduleName)
    {
        var moduleTrackingState = GetModuleTrackingState(moduleName);
        if (moduleTrackingState != null)
        {
            moduleTrackingState.ModuleInitializationStatus = ModuleInitializationStatus.Initialized;
        }

        _logger?.Log(string.Format(CultureInfo.CurrentCulture, "'{0}' module initialized.", moduleName));
    }

    private ModuleTrackingState? GetModuleTrackingState(string moduleName)
    {
        switch (moduleName)
        {
            default:
                return null;
        }
    }
}