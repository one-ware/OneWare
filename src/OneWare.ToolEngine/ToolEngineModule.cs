using OneWare.Essentials.Services;
using OneWare.ToolEngine.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.ToolEngine;

public class ToolEngineModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IToolService, ToolService>();
        containerRegistry.RegisterSingleton<IToolExecutionDispatcherService, ToolExecutionDispatcherService>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var settingsService = containerProvider.Resolve<ISettingsService>();
        settingsService.RegisterSettingSubCategory("Tool", "Execution Strategy");
    }
}