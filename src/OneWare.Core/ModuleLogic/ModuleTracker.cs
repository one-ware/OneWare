using System.Globalization;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using Prism.Modularity;

namespace OneWare.Core.ModuleLogic;

internal class ModuleTracker : IModuleTracker
{
    public ModuleTracker(IModuleCatalog moduleCatalog, IModuleManager moduleManager)
    {
        ModuleCatalog = moduleCatalog;
        ModuleManager = moduleManager;
    }

    public IModuleCatalog ModuleCatalog { get; }

    public IModuleManager ModuleManager { get; }

    public void RecordModuleLoaded(string moduleName)
    {
        AppServices.Logger.LogInformation(string.Format(CultureInfo.CurrentCulture, "'{0}' module loaded.", moduleName));
    }

    public void RecordModuleConstructed(string moduleName)
    {
        var moduleTrackingState = GetModuleTrackingState(moduleName);
        if (moduleTrackingState != null)
            moduleTrackingState.ModuleInitializationStatus = ModuleInitializationStatus.Constructed;

        AppServices.Logger.LogInformation(string.Format(CultureInfo.CurrentCulture, "'{0}' module constructed.", moduleName));
    }

    public void RecordModuleInitialized(string moduleName)
    {
        var moduleTrackingState = GetModuleTrackingState(moduleName);
        if (moduleTrackingState != null)
            moduleTrackingState.ModuleInitializationStatus = ModuleInitializationStatus.Initialized;

        AppServices.Logger.LogInformation(string.Format(CultureInfo.CurrentCulture, "'{0}' module initialized.", moduleName));
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