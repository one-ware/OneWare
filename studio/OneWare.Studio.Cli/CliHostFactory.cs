using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneWare.Core.ModuleLogic;
using OneWare.Core.Services;
using OneWare.Essentials.Services;
using OneWare.Settings;

internal static class CliHostFactory
{
    public static CliHostBuilderContext Create()
    {
        var services = new ServiceCollection();
        var paths = new Paths("OneWare Studio", "avares://OneWare.Studio/Assets/icon.ico");
        var moduleCatalog = new OneWareCliModuleCatalog();
        var moduleManager = new OneWareCliModuleManager(moduleCatalog);
        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger("OneWare.Studio.Cli");

        moduleManager.SetLogger(logger);

        services.AddSingleton<IPaths>(paths);
        services.AddSingleton<IHttpService, HttpService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton(moduleCatalog);
        services.AddSingleton(moduleManager);
        services.AddSingleton<ILoggerFactory>(loggerFactory);
        services.AddSingleton<ILogger>(logger);

        return new CliHostBuilderContext(services, paths, moduleCatalog, moduleManager, logger);
    }
}

internal sealed class CliHostContext(
    ServiceProvider serviceProvider,
    OneWareCliModuleCatalog moduleCatalog,
    OneWareCliModuleManager moduleManager) : IDisposable
{
    public ServiceProvider ServiceProvider { get; } = serviceProvider;
    public OneWareCliModuleCatalog ModuleCatalog { get; } = moduleCatalog;
    public OneWareCliModuleManager ModuleManager { get; } = moduleManager;

    public void Dispose()
    {
        ServiceProvider.Dispose();
    }
}

internal sealed class CliHostBuilderContext(
    ServiceCollection services,
    IPaths paths,
    OneWareCliModuleCatalog moduleCatalog,
    OneWareCliModuleManager moduleManager,
    ILogger logger)
{
    public ServiceCollection Services { get; } = services;
    public IPaths Paths { get; } = paths;
    public OneWareCliModuleCatalog ModuleCatalog { get; } = moduleCatalog;
    public OneWareCliModuleManager ModuleManager { get; } = moduleManager;
    public ILogger Logger { get; } = logger;

    public CliHostContext Build()
    {
        ModuleManager.RegisterModuleServices(Services, ModuleCatalog.Modules);

        var serviceProvider = Services.BuildServiceProvider();
        ContainerLocator.SetContainer(serviceProvider);
        return new CliHostContext(serviceProvider, ModuleCatalog, ModuleManager);
    }
}
