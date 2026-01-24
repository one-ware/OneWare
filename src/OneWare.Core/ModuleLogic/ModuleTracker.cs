using System.Globalization;
using OneWare.Essentials.Services;
using Microsoft.Extensions.Logging;

namespace OneWare.Core.ModuleLogic;

internal class ModuleTracker : IModuleTracker
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
        _logger.Log(string.Format(CultureInfo.CurrentCulture, "'{0}' module constructed.", moduleName));
    }

    public void RecordModuleInitialized(string moduleName)
    {
        _logger?.Log(string.Format(CultureInfo.CurrentCulture, "'{0}' module initialized.", moduleName));
    }
}
