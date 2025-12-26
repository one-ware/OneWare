using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Prism.Ioc;
using Splat;
using ILogger = OneWare.Essentials.Services.ILogger;

namespace OneWare.ToolEngine.Services;

public class ToolService : IToolService
{
    private readonly List<EnvironmentDescription> _tools = new();
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;

    public ToolService(ISettingsService settingsService, ILogger logger)
    {
        _settingsService = settingsService 
                           ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private void RegisterToolInSettings(EnvironmentDescription description)
    {
        // var setting = new ListBoxSetting("Tool Strategy", "Native", "Docker");
        var dispatcherService = ContainerLocator.Container.Resolve<IToolExecutionDispatcherService>();
        // TODO: Hier mal rüber schauen 
        dispatcherService.Register(description.Key);

        var strategies = dispatcherService.GetStrategyKeys(description.Key);
        
        if (strategies.Length == 0) {
            _logger.Warning($"No strategies found for Tool: {description.Key}");
            return;
        }
        
        var setting = new ComboBoxSetting(description.Name, strategies[0], strategies.ToArray());
        _settingsService.RegisterSetting("Tools", "Execution Strategy", description.Key, setting);
    }
    
    public void Register(EnvironmentDescription description)
    {
        RegisterToolInSettings(description);
        _tools.Add(description);
    }

    public void Unregister(EnvironmentDescription description)
    {
        _tools.Remove(description);
    }

    public void Unregister(string toolKey)
    {
        var tool = _tools.FirstOrDefault(t => t.Key == toolKey);
        
        if (tool is null)
        {
            throw new InvalidOperationException($"Tool with key '{toolKey}' not found.");
        }
        
        Unregister(tool);
    }

    public IReadOnlyList<EnvironmentDescription> GetAllTools()
    {
        return _tools.AsReadOnly();
    }

    public ToolConfiguration GetGlobalToolConfiguration()
    {
        var config = new ToolConfiguration();
        foreach (var tool in _tools)
        {
            config.StrategyMapping.Add(tool.Key, _settingsService.GetSettingValue<string>(tool.Key));
        }
        return config;
    }

    public void UpdateSettings()
    {
        
    }
}