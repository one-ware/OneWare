using System.Collections.ObjectModel;
using OneWare.Essentials.Models;
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
        settingsService.RegisterSettingCategory("Binary Management", iconKey: "VSImageLib.BinaryManagement_16x");
        settingsService.RegisterSettingSubCategory("Binary Management", "Execution Strategy");

        var toolService = containerProvider.Resolve<IToolService>();
        var executionDispatcherService = containerProvider.Resolve<IToolExecutionDispatcherService>();
        
        
    }
}