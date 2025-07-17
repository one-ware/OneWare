using System.Runtime.InteropServices;
using OneWare.Core.Data;
using OneWare.Essentials.Services;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Sinks.SystemConsole.Themes;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LoggerExtensions = Microsoft.Extensions.Logging.LoggerExtensions;

namespace OneWare.Core.Services;

public class LoggerBuilder : ILoggerFactory
{
    public ILogger CreateLogger(string fileDirectory)
    {
        var serilogLogger = new LoggerConfiguration()
            #if DEBUG
            .WriteTo.Console(
                restrictedToMinimumLevel: LogEventLevel.Debug,
                outputTemplate: "[{Timestamp:HH:mm:ss}]: [{Level}] {Message}{NewLine}{Exception}",
                theme: SystemConsoleTheme.Colored)
            #endif
            .WriteTo.File(Path.Combine(fileDirectory, "IDELog_.txt"),
                restrictedToMinimumLevel: LogEventLevel.Information,
                outputTemplate: "[{Timestamp:dd-MM-yyyy HH:mm:ss}]: [{Level}] {Message}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var msLogger = new SerilogLoggerFactory(serilogLogger)
            .CreateLogger("Logger");
        
        LoggerExtensions.LogInformation(msLogger,
            $"Version: {Global.VersionCode} " +
                    $"OS: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}");
        
        return msLogger;
    }
}