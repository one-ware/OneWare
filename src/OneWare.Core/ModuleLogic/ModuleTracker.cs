using System.Globalization;
using OneWare.Shared.Enums;
using OneWare.Shared.Services;

namespace OneWare.Core.ModuleLogic;

class ModuleTracker : IModuleTracker
{
    private readonly ILogger _logger;

    public ModuleTracker(ILogger logger)
    {
        _logger = logger;
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