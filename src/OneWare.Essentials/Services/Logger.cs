using Acornima.Ast;
using Microsoft.Extensions.Logging;

namespace OneWare.Essentials.Services;

public static class AppServices
{
    private static ILogger? _logger;
    
    public static ILogger Logger
    {
        get
        {
            _logger ??= LoggerFactory.Create(builder => builder
                    .AddConsole())
                    .CreateLogger("Logger");
            
            return _logger;
        }
        private set => _logger = value;
    }

    public static void InitLogger(ILogger logger)
    {
        Logger = logger;
    }
}