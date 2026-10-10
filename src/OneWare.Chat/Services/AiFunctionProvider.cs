using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Avalonia.Threading;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Chat.Services;

public class AiFunctionProvider(
    IProjectExplorerService projectExplorerService,
    IMainDockService dockService,
    IErrorService errorService,
    ITerminalManagerService terminalManagerService,
    IWindowService windowService,
    IPaths paths,
    ILogger logger,
    AiFileEditService aiFileEditService) : IAiFunctionProvider
{
    private readonly Lock _registrationLock = new();
    private readonly List<IOneWareAiFunction> _registeredFunctions = [];
    private readonly List<string> _promptAdditions = [];
    private readonly List<OneWareAiAgent> _registeredAgents = [];
    private readonly List<OneWareAiSkill> _registeredSkills = [];
    private readonly List<string> _registeredSkillDirectories = [];
    private readonly ConcurrentDictionary<string, ActiveFunction> _activeFunctions = new();
    private bool _builtInsRegistered;

    public event EventHandler<AiFunctionStartedEvent>? FunctionStarted;
    public event EventHandler<AiFunctionCompletedEvent>? FunctionCompleted;
    public event EventHandler<AiFunctionProgressEvent>? FunctionProgress;

    public void RegisterFunction(IOneWareAiFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);

        lock (_registrationLock)
        {
            _registeredFunctions.RemoveAll(x => string.Equals(x.Name, function.Name, StringComparison.Ordinal));
            _registeredFunctions.Add(function);
        }
    }

    public void RegisterPromptAddition(string promptAddition)
    {
        if (string.IsNullOrWhiteSpace(promptAddition)) return;
        var trimmed = promptAddition.Trim();

        lock (_registrationLock)
        {
            if (_promptAdditions.Contains(trimmed, StringComparer.Ordinal))
                return;

            _promptAdditions.Add(trimmed);
        }
    }

    public IReadOnlyCollection<string> GetPromptAdditions()
    {
        lock (_registrationLock)
        {
            return _promptAdditions.ToArray();
        }
    }

    public void RegisterAgent(OneWareAiAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (string.IsNullOrWhiteSpace(agent.Name))
            throw new ArgumentException("An agent needs a name.", nameof(agent));

        lock (_registrationLock)
        {
            _registeredAgents.RemoveAll(x => string.Equals(x.Name, agent.Name, StringComparison.OrdinalIgnoreCase));
            _registeredAgents.Add(agent);
        }
    }

    public IReadOnlyCollection<OneWareAiAgent> GetAgents()
    {
        lock (_registrationLock)
        {
            return _registeredAgents.ToArray();
        }
    }

    public void RegisterSkill(OneWareAiSkill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);

        if (string.IsNullOrWhiteSpace(skill.Name))
            throw new ArgumentException("A skill needs a name.", nameof(skill));

        if (string.IsNullOrWhiteSpace(skill.Description))
            throw new ArgumentException($"Skill '{skill.Name}' needs a description.", nameof(skill));

        if (string.IsNullOrWhiteSpace(skill.Instructions))
            throw new ArgumentException($"Skill '{skill.Name}' needs instructions.", nameof(skill));

        lock (_registrationLock)
        {
            _registeredSkills.RemoveAll(x => string.Equals(x.Name, skill.Name, StringComparison.OrdinalIgnoreCase));
            _registeredSkills.Add(skill);
        }
    }

    public IReadOnlyCollection<OneWareAiSkill> GetSkills()
    {
        lock (_registrationLock)
        {
            return _registeredSkills.ToArray();
        }
    }

    public void RegisterSkillDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("A skill directory needs a path.", nameof(directory));

        var fullPath = Path.GetFullPath(directory);

        lock (_registrationLock)
        {
            if (!_registeredSkillDirectories.Contains(fullPath, StringComparer.Ordinal))
                _registeredSkillDirectories.Add(fullPath);
        }
    }

    public IReadOnlyCollection<string> GetSkillDirectories()
    {
        OneWareAiSkill[] skills;
        string[] pluginDirectories;
        lock (_registrationLock)
        {
            skills = _registeredSkills.ToArray();
            pluginDirectories = _registeredSkillDirectories.ToArray();
        }

        var directories = new List<string>();

        foreach (var directory in pluginDirectories)
        {
            if (Directory.Exists(directory))
                directories.Add(directory);
            else
                logger.Warning($"Skill directory does not exist: {directory}");
        }

        // All skills defined in code share one generated discovery root.
        if (TryWriteInlineSkills(skills)) directories.Add(InlineSkillRoot);

        return directories.Distinct(StringComparer.Ordinal).ToArray();
    }

    private string InlineSkillRoot => Path.Combine(paths.AppDataDirectory, "AI", "Skills");

    /// <summary>
    /// Materializes all skills defined in code below <see cref="InlineSkillRoot"/> and removes
    /// directories of skills that are no longer registered (e.g. after a plugin was uninstalled or
    /// a skill was renamed), because the whole root is handed to the AI backend for discovery.
    /// </summary>
    private bool TryWriteInlineSkills(IReadOnlyCollection<OneWareAiSkill> skills)
    {
        var root = InlineSkillRoot;

        if (skills.Count == 0)
        {
            TryDeleteInlineSkillDirectories(root, []);
            return false;
        }

        var written = false;
        var expectedDirectoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var skill in skills)
        {
            var directoryName = SanitizeSkillName(skill.Name);
            if (TryWriteInlineSkill(skill, Path.Combine(root, directoryName)))
            {
                expectedDirectoryNames.Add(directoryName);
                written = true;
            }
        }

        TryDeleteInlineSkillDirectories(root, expectedDirectoryNames);

        return written;
    }

    private void TryDeleteInlineSkillDirectories(string root, ICollection<string> expectedDirectoryNames)
    {
        try
        {
            if (!Directory.Exists(root)) return;

            foreach (var directory in Directory.EnumerateDirectories(root))
            {
                if (expectedDirectoryNames.Contains(Path.GetFileName(directory))) continue;

                Directory.Delete(directory, true);
            }
        }
        catch (Exception e)
        {
            logger.Warning("Cleaning up unregistered skills failed", e);
        }
    }

    private bool TryWriteInlineSkill(OneWareAiSkill skill, string skillDirectory)
    {
        try
        {
            Directory.CreateDirectory(skillDirectory);

            var content = $"""
                           ---
                           name: {ToYamlString(skill.Name)}
                           description: {ToYamlString(skill.Description)}
                           ---

                           {skill.Instructions.Trim()}

                           """;

            var filePath = Path.Combine(skillDirectory, "SKILL.md");

            // Only rewrite on change so the file timestamp stays stable across restarts.
            if (File.Exists(filePath) && File.ReadAllText(filePath) == content) return true;

            File.WriteAllText(filePath, content);
            return true;
        }
        catch (Exception e)
        {
            logger.Error($"Writing skill '{skill.Name}' failed", e);
            return false;
        }
    }

    private static string SanitizeSkillName(string name)
    {
        var sanitized = new string(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray())
            .Trim('-');

        return string.IsNullOrWhiteSpace(sanitized) ? "skill" : sanitized.ToLowerInvariant();
    }

    /// <summary>
    /// Emits a double-quoted single-line YAML scalar, so descriptions containing ":", "#" or line
    /// breaks cannot break the front matter (which would make the AI backend drop the skill).
    /// </summary>
    private static string ToYamlString(string value)
    {
        var singleLine = value.Replace("\r", string.Empty).Replace("\n", " ").Trim();
        var escaped = singleLine.Replace("\\", "\\\\").Replace("\"", "\\\"");

        return $"\"{escaped}\"";
    }

    public bool? IsFunctionReadOnly(string functionName)
    {
        EnsureBuiltInsRegistered();
        lock (_registrationLock)
        {
            return _registeredFunctions
                .FirstOrDefault(f => string.Equals(f.Name, functionName, StringComparison.Ordinal))
                ?.IsReadOnly;
        }
    }

    public Func<AIFunctionArguments, string?>? GetConfirmationCheck(string functionName)
    {
        EnsureBuiltInsRegistered();
        lock (_registrationLock)
        {
            return _registeredFunctions
                .FirstOrDefault(f => string.Equals(f.Name, functionName, StringComparison.Ordinal))
                ?.ConfirmationCheck;
        }
    }

    public ICollection<AIFunction> GetTools()
    {
        EnsureBuiltInsRegistered();

        List<IOneWareAiFunction> functions;
        lock (_registrationLock)
        {
            functions = _registeredFunctions.ToList();
        }

        var tools = new List<AIFunction>(functions.Count);
        foreach (var definition in functions)
        {
            var baseFunction = AIFunctionFactory.Create(
                definition.Handler,
                new AIFunctionFactoryOptions
                {
                    Name = definition.Name,
                    Description = definition.Description,
                    MarshalResult = MarshalResult
                });

            tools.Add(new RegisteredOneWareAiFunction(this, baseFunction, definition));
        }

        return tools;
    }

    /// <summary>
    /// Keeps <see cref="AIContent"/> results (e.g. <see cref="DataContent"/> images) intact so AI backends
    /// can forward them to the model as binary content; everything else is JSON-serialized as by default.
    /// </summary>
    private static ValueTask<object?> MarshalResult(object? result, Type? type, CancellationToken cancellationToken)
    {
        if (result is AIContent or IEnumerable<AIContent>)
            return new ValueTask<object?>(result);

        if (type == null || type == typeof(void))
            return new ValueTask<object?>((object?)null);

        return new ValueTask<object?>(
            JsonSerializer.SerializeToElement(result, AIJsonUtilities.DefaultOptions.GetTypeInfo(type)));
    }

    public void CancelActiveFunctions()
    {
        foreach (var id in _activeFunctions.Keys)
            CancelFunction(id);
    }

    public void CancelActiveFunctions(string sessionId)
    {
        foreach (var (id, function) in _activeFunctions)
        {
            if (string.Equals(function.SessionId, sessionId, StringComparison.Ordinal))
                CancelFunction(id);
        }
    }

    public void CancelFunction(string id)
    {
        if (!_activeFunctions.TryGetValue(id, out var function)) return;

        try
        {
            function.Cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The function completed while cancellation was being requested.
        }
    }

    private void EnsureBuiltInsRegistered()
    {
        lock (_registrationLock)
        {
            if (_builtInsRegistered) return;
            _builtInsRegistered = true;
        }

        AiBuiltInFunctions.Register(
            this,
            projectExplorerService,
            dockService,
            errorService,
            terminalManagerService,
            windowService,
            aiFileEditService);
    }

    private sealed record ActiveFunction(CancellationTokenSource Cancellation, string? SessionId);

    private async Task NotifyFunctionStartedAsync(string id, string? sessionId, string functionName,
        string toolName, string? toolCallId, string? detail = null)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
            FunctionStarted?.Invoke(this, new AiFunctionStartedEvent
            {
                Id = id,
                SessionId = sessionId,
                FunctionName = functionName,
                ToolName = toolName,
                ToolCallId = toolCallId,
                Detail = detail
            }));
    }

    private static readonly ConcurrentDictionary<(Type Type, string Name), PropertyInfo?> BackendContextProperties =
        new();

    /// <summary>
    /// Reads a string the AI backend attached to this invocation, such as its <c>ToolCallId</c> or
    /// <c>SessionId</c>. Backends pass their invocation context in <see cref="AIFunctionArguments.Context"/>;
    /// the shape of that context is backend specific, so it is only probed for a property of that name.
    /// </summary>
    internal static string? TryGetBackendContextValue(AIFunctionArguments arguments, string propertyName)
    {
        if (arguments.Context == null) return null;

        foreach (var value in arguments.Context.Values)
        {
            if (value == null) continue;

            var property = BackendContextProperties.GetOrAdd((value.GetType(), propertyName),
                key => key.Type.GetProperty(key.Name, BindingFlags.Public | BindingFlags.Instance));

            if (property?.PropertyType != typeof(string)) continue;

            if (property.GetValue(value) is string text && !string.IsNullOrWhiteSpace(text))
                return text;
        }

        return null;
    }

    private async Task NotifyFunctionCompletedAsync(string id, string? sessionId, Exception? exception = null)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
            FunctionCompleted?.Invoke(this, new AiFunctionCompletedEvent
            {
                Id = id,
                SessionId = sessionId,
                Result = exception == null,
                ToolOutput = exception is OperationCanceledException ? "Cancelled." : exception?.ToString()
            }));
    }

    private void RaiseFunctionProgress(string id, string? sessionId, string output)
    {
        Dispatcher.UIThread.Post(() =>
            FunctionProgress?.Invoke(this, new AiFunctionProgressEvent
            {
                Id = id,
                SessionId = sessionId,
                Output = output
            }));
    }

    private sealed class RegisteredOneWareAiFunction(
        AiFunctionProvider provider,
        AIFunction innerFunction,
        IOneWareAiFunction definition) : DelegatingAIFunction(innerFunction)
    {
        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            var friendlyName = string.IsNullOrWhiteSpace(definition.FriendlyName)
                ? definition.Name
                : definition.FriendlyName;

            var detail = definition.DetailExtractor?.Invoke(arguments);
            var id = Guid.NewGuid().ToString();
            using var functionCancellationSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var sessionId = TryGetBackendContextValue(arguments, "SessionId");
            provider._activeFunctions[id] = new ActiveFunction(functionCancellationSource, sessionId);

            var context = new AiFunctionInvocationContext(id,
                output => provider.RaiseFunctionProgress(id, sessionId, output))
            {
                SessionId = sessionId
            };
            Exception? exception = null;
            try
            {
                await provider.NotifyFunctionStartedAsync(id, sessionId, friendlyName!, definition.Name,
                    TryGetBackendContextValue(arguments, "ToolCallId"), detail);

                if (definition.RunOnUiThread)
                {
                    return await Dispatcher.UIThread.InvokeAsync(async () =>
                        await InvokeDefinitionAsync(context, arguments, functionCancellationSource.Token));
                }

                return await InvokeDefinitionAsync(context, arguments, functionCancellationSource.Token);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Only this tool call was cancelled (e.g. via its stop button) — report it to the
                // model as a result instead of failing the whole chat turn.
                exception = ex;
                return "The tool call was stopped by the user before it finished.";
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                provider._activeFunctions.TryRemove(id, out _);
                await provider.NotifyFunctionCompletedAsync(id, sessionId, exception);
            }
        }

        private ValueTask<object?> InvokeDefinitionAsync(AiFunctionInvocationContext context,
            AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            return definition.InvocationHandler != null
                ? definition.InvocationHandler(context, arguments, cancellationToken)
                : base.InvokeCoreAsync(arguments, cancellationToken);
        }
    }
}
