using System.Collections.ObjectModel;
using OneWare.Essentials.ToolEngine;

namespace OneWare.Essentials.Services;

public interface IToolService
{
    /// <summary>
    /// Registers a tool. At least one execution strategy must become available for it separately -
    /// via <see cref="RegisterUniversalStrategy"/>, via <see cref="RegisterStrategy"/> naming this
    /// tool's key in its <c>supportedToolKeys</c>, or via a strategy whose key appears in this tool's
    /// <see cref="ToolContext.PreferredStrategyKeys"/>. Any of these can happen before or after this
    /// call - the Settings entry for the tool is built as soon as the tool and at least one available
    /// strategy are both known, regardless of order.
    /// </summary>
    void Register(ToolContext description);

    /// <summary>
    /// Unregisters a tool by context.
    /// </summary>
    void Unregister(ToolContext description);

    /// <summary>
    /// Unregisters a tool by key.
    /// </summary>
    void Unregister(string toolKey);

    /// <summary>
    /// Returns all registered tools.
    /// </summary>
    ObservableCollection<ToolContext> GetAllTools();

    /// <summary>
    /// Returns the global tool configuration.
    /// </summary>
    ToolConfiguration GetGlobalToolConfiguration();

    /// <summary>
    /// Registers an execution strategy globally, under its own
    /// <see cref="IToolExecutionStrategy.GetStrategyKey"/>. Without <paramref name="supportedToolKeys"/>,
    /// the strategy is only available to a tool that explicitly lists this key in its own
    /// <see cref="ToolContext.PreferredStrategyKeys"/> - a tool-side opt-in. Pass
    /// <paramref name="supportedToolKeys"/> for the reverse, strategy-side opt-in: the tool keys this
    /// strategy explicitly supports, regardless of whether those tools listed it as preferred - e.g. a
    /// Docker extension attaching itself to tools declared by a module it has no reference to. A
    /// strategy available through either path is also eligible to become a tool's default pick; see
    /// <see cref="ToolContext.PreferredStrategyKeys"/>.
    /// </summary>
    public void RegisterStrategy(IToolExecutionStrategy strategy, IReadOnlyCollection<string>? supportedToolKeys = null);

    /// <summary>
    /// Registers an execution strategy that is available to every tool, regardless of what any tool or
    /// strategy declares. Intended for a generic fallback such as a native-process strategy, which can
    /// run any tool by executable name and needs no tool-specific wiring.
    /// </summary>
    public void RegisterUniversalStrategy(IToolExecutionStrategy strategy);

    /// <summary>
    /// Unregisters an execution strategy by key, removing it for every tool it was available to.
    /// </summary>
    public void UnregisterStrategy(string strategyKey);

    /// <summary>
    /// Returns all strategies for a tool.
    /// </summary>
    public IReadOnlyList<IToolExecutionStrategy> GetStrategies(string toolKey);

    /// <summary>
    /// Returns strategy keys for a tool.
    /// </summary>
    public string[] GetStrategyKeys(string toolKey);

    /// <summary>
    /// Returns the active strategy for a tool.
    /// </summary>
    IToolExecutionStrategy GetStrategy(string toolKey);

    /// <summary>
    /// Returns the strategy registered under a specific strategy key, if it is actually available to
    /// the given tool (universal, explicitly supporting this tool key, or listed in the tool's
    /// <see cref="ToolContext.PreferredStrategyKeys"/>), bypassing the tool's configured strategy
    /// setting entirely. Used to force a specific call onto a specific strategy
    /// (see <see cref="OneWare.Essentials.ToolEngine.ToolCommand.ForcedStrategyKey"/>).
    /// </summary>
    /// <returns>The matching strategy, or <c>null</c> if it isn't available to the tool.</returns>
    IToolExecutionStrategy? TryGetStrategy(string toolKey, string strategyKey);

    /// <summary>
    /// Returns the effective strategy configuration for a tool: the plugin-declared defaults from its
    /// <see cref="ToolContext.StrategyConfiguration"/>, with any user-set overrides applied on top.
    /// A strategy implementation resolves this itself (e.g. via <c>ContainerLocator</c>, the same way
    /// <c>NativeStrategy</c> resolves <c>IChildProcessService</c>) using the tool key it was asked to run,
    /// and reads whichever keys it recognizes by convention (e.g. "docker.image").
    /// </summary>
    IReadOnlyDictionary<string, string> GetStrategyConfiguration(string toolKey);

    /// <summary>
    /// Returns the subset of <see cref="GetStrategyConfiguration(string)"/> whose keys start with
    /// <paramref name="prefix"/> (e.g. "docker." to get only that strategy's keys). Convenience filter for
    /// a strategy that only cares about its own namespace within the tool's configuration.
    /// </summary>
    IReadOnlyDictionary<string, string> GetStrategyConfiguration(string toolKey, string prefix);

    /// <summary>
    /// Sets a user override for a single strategy configuration key on a tool, taking precedence over the
    /// owning plugin's declared default for that key. Other keys are left untouched.
    /// </summary>
    void SetStrategyConfigurationValue(string toolKey, string configKey, string value);

    /// <summary>
    /// Returns the fully merged strategy configuration for a specific call: the tool's plugin-declared
    /// defaults, overridden by any user Settings override (both from <see cref="GetStrategyConfiguration"/>),
    /// overridden in turn by <see cref="ToolCommand.StrategyConfigurationOverrides"/> for this call only.
    /// A strategy implementation should call this instead of merging the layers itself.
    /// </summary>
    IReadOnlyDictionary<string, string> GetEffectiveStrategyConfiguration(ToolCommand command);
}
