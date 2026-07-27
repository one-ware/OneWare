using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ToolEngine;
using OneWare.Essentials.ToolEngine.Strategies;

namespace OneWare.ToolEngine.Services;

public class ToolService : IToolService
{
    private readonly ILogger _logger;
    private readonly ISettingsService _settingsService;
    private readonly ObservableCollection<ToolContext> _tools = new();
    private readonly Dictionary<string, IToolExecutionStrategy> _strategies = new();
    private readonly HashSet<string> _universalStrategyKeys = new();
    private readonly Dictionary<string, HashSet<string>> _strategySupportedToolKeys = new();

    public ToolService(ISettingsService settingsService, ILogger logger)
    {
        _settingsService = settingsService
                           ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Register(ToolContext description)
    {
        if (_tools.Any(t => t.Key == description.Key))
            throw new InvalidOperationException($"Tool with key '{description.Key}' is already registered.");

        _tools.Add(description);
        SyncStrategyOptions(description.Key);
    }


    public void Unregister(ToolContext description)
    {
        _tools.Remove(description);
    }

    public void Unregister(string toolKey)
    {
        var tool = _tools.FirstOrDefault(t => t.Key == toolKey);

        if (tool is null) throw new InvalidOperationException($"Tool with key '{toolKey}' not found.");

        Unregister(tool);
    }

    public ObservableCollection<ToolContext> GetAllTools()
    {
        return _tools;
    }

    public ToolConfiguration GetGlobalToolConfiguration()
    {
        var config = new ToolConfiguration();
        foreach (var tool in _tools)
            config.StrategyMapping.Add(tool.Key, _settingsService.GetSettingValue<string>(tool.Key));
        return config;
    }

    public void RegisterStrategy(IToolExecutionStrategy strategy, IReadOnlyCollection<string>? supportedToolKeys = null)
    {
        var strategyKey = strategy.GetStrategyKey();
        _strategies[strategyKey] = strategy;

        if (supportedToolKeys is { Count: > 0 })
        {
            if (!_strategySupportedToolKeys.TryGetValue(strategyKey, out var supported))
            {
                supported = new HashSet<string>();
                _strategySupportedToolKeys[strategyKey] = supported;
            }

            foreach (var toolKey in supportedToolKeys) supported.Add(toolKey);
        }

        foreach (var tool in _tools) SyncStrategyOptions(tool.Key);
    }

    public void RegisterUniversalStrategy(IToolExecutionStrategy strategy)
    {
        _strategies[strategy.GetStrategyKey()] = strategy;
        _universalStrategyKeys.Add(strategy.GetStrategyKey());

        foreach (var tool in _tools) SyncStrategyOptions(tool.Key);
    }

    /// <summary>
    /// Builds the tool's Settings dropdown the first time both its <see cref="ToolContext"/> and at least
    /// one strategy are known, and keeps it in sync afterwards. <see cref="Register"/> and
    /// <see cref="RegisterStrategy"/> can run in either order - whichever call completes the pair builds
    /// the setting. Once built, only the selectable options are ever updated here, never the active value,
    /// so a tool's runtime behavior never changes without the user explicitly picking a new strategy.
    /// </summary>
    private void SyncStrategyOptions(string toolKey)
    {
        var currentKeys = GetStrategyKeys(toolKey);
        if (currentKeys.Length == 0) return;

        if (_settingsService.HasSetting(toolKey))
        {
            if (_settingsService.GetSetting(toolKey) is ComboBoxSetting combo &&
                !combo.Options.SequenceEqual(currentKeys))
                combo.Options = currentKeys.Cast<object>().ToArray();
            return;
        }

        var description = _tools.FirstOrDefault(t => t.Key == toolKey);
        if (description is null) return;

        var defaultStrategy = description.PreferredStrategyKeys.FirstOrDefault(currentKeys.Contains) ?? currentKeys[0];

        var setting = new ComboBoxSetting(description.Name, defaultStrategy, currentKeys.Cast<object>().ToArray());
        _settingsService.RegisterSetting("Binary Management", "Execution Strategy", toolKey, setting);
    }

    public void UnregisterStrategy(string strategyKey)
    {
        _strategies.Remove(strategyKey);
        _universalStrategyKeys.Remove(strategyKey);
        _strategySupportedToolKeys.Remove(strategyKey);

        foreach (var tool in _tools) SyncStrategyOptions(tool.Key);
    }

    /// <summary>
    /// Computes the strategy keys available to a tool from all three opt-in paths: universal
    /// strategies, strategies that explicitly declared this tool key as supported, and strategies
    /// named in the tool's own <see cref="ToolContext.PreferredStrategyKeys"/>.
    /// </summary>
    public string[] GetStrategyKeys(string toolKey)
    {
        var keys = new HashSet<string>(_universalStrategyKeys);

        foreach (var (strategyKey, supportedToolKeys) in _strategySupportedToolKeys)
            if (supportedToolKeys.Contains(toolKey)) keys.Add(strategyKey);

        var tool = _tools.FirstOrDefault(t => t.Key == toolKey);
        if (tool is not null)
            foreach (var strategyKey in tool.PreferredStrategyKeys)
                if (_strategies.ContainsKey(strategyKey)) keys.Add(strategyKey);

        return keys.ToArray();
    }

    public IReadOnlyList<IToolExecutionStrategy> GetStrategies(string toolKey)
    {
        return GetStrategyKeys(toolKey).Select(key => _strategies[key]).ToList();
    }

    public IToolExecutionStrategy GetStrategy(string toolKey)
    {
        if (!_settingsService.HasSetting(toolKey))
        {
            throw new InvalidOperationException(
                $"No Setting  for key '{toolKey}' was found. Register Tool first bevor you are using it");
        }

        var strategyKey = _settingsService.GetSettingValue<string>(toolKey);
        var strategy = TryGetStrategy(toolKey, strategyKey);
        if (strategy is not null) return strategy;

        _logger.LogError($"No execution strategy found for tool '{toolKey}' and strategy '{strategyKey}'");
        _logger.LogError("Using default strategy");

        var fallback = TryGetStrategy(toolKey, NativeStrategy.ToolKey);
        if (fallback is not null) return fallback;

        throw new InvalidOperationException($"No strategy with key '{toolKey}' was found.");
    }

    public IToolExecutionStrategy? TryGetStrategy(string toolKey, string strategyKey)
    {
        if (!GetStrategyKeys(toolKey).Contains(strategyKey)) return null;

        return _strategies.GetValueOrDefault(strategyKey);
    }

    public IReadOnlyDictionary<string, string> GetStrategyConfiguration(string toolKey)
    {
        var defaults = _tools.FirstOrDefault(t => t.Key == toolKey)?.StrategyConfiguration
                       ?? new Dictionary<string, string>();

        var overrideKey = StrategyConfigOverrideKey(toolKey);
        if (!_settingsService.HasSetting(overrideKey)) return defaults;

        var overrides = _settingsService.GetSettingValue<Dictionary<string, string>>(overrideKey);
        if (overrides.Count == 0) return defaults;

        var merged = new Dictionary<string, string>(defaults);
        foreach (var (key, value) in overrides) merged[key] = value;
        return merged;
    }

    public IReadOnlyDictionary<string, string> GetStrategyConfiguration(string toolKey, string prefix)
    {
        return GetStrategyConfiguration(toolKey)
            .Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public void SetStrategyConfigurationValue(string toolKey, string configKey, string value)
    {
        var overrideKey = StrategyConfigOverrideKey(toolKey);
        if (!_settingsService.HasSetting(overrideKey))
            _settingsService.Register(overrideKey, new Dictionary<string, string>());

        // Copy rather than mutate in place: Setting.Value change notifications only fire on a new reference.
        var overrides = new Dictionary<string, string>(
            _settingsService.GetSettingValue<Dictionary<string, string>>(overrideKey))
        {
            [configKey] = value
        };

        _settingsService.SetSettingValue(overrideKey, overrides);
    }

    public IReadOnlyDictionary<string, string> GetEffectiveStrategyConfiguration(ToolCommand command)
    {
        var configuration = GetStrategyConfiguration(command.ToolName);
        if (command.StrategyConfigurationOverrides.Count == 0) return configuration;

        var merged = new Dictionary<string, string>(configuration);
        foreach (var (key, value) in command.StrategyConfigurationOverrides) merged[key] = value;
        return merged;
    }

    private static string StrategyConfigOverrideKey(string toolKey) => $"{toolKey}_StrategyConfigOverrides";

    public void UpdateSettings()
    {
    }
}