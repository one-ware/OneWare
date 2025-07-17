using Microsoft.Extensions.Logging;

namespace OneWare.Essentials.Services;

public interface ILoggerFactory
{
    public ILogger CreateLogger(string fileDirectory);
}