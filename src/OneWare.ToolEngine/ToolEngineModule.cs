using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Services;
using OneWare.ToolEngine.Services;

namespace OneWare.ToolEngine;

public class ToolEngineModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IToolService, ToolService>();
        services.AddSingleton<IToolExecutionDispatcherService, ToolExecutionDispatcherService>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var settingsService = serviceProvider.Resolve<ISettingsService>();
        settingsService.RegisterSettingCategory("Binary Management", iconKey: "VSImageLib.BinaryManagement_16x");
        settingsService.RegisterSettingSubCategory("Binary Management", "Execution Strategy");

        _ = serviceProvider.Resolve<IToolService>();
        _ = serviceProvider.Resolve<IToolExecutionDispatcherService>();
    }
}