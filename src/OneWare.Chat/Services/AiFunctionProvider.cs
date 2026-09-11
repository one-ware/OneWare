using System.Collections.Concurrent;
using System.IO;
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
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeFunctions = new();
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
                definition.Name,
                definition.Description);

            tools.Add(new RegisteredOneWareAiFunction(this, baseFunction, definition));
        }

        return tools;
    }

    public void CancelActiveFunctions()
    {
        foreach (var id in _activeFunctions.Keys)
            CancelFunction(id);
    }

    public void CancelFunction(string id)
    {
        if (!_activeFunctions.TryGetValue(id, out var cancellationSource)) return;

        try
        {
            cancellationSource.Cancel();
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

    private async Task NotifyFunctionStartedAsync(string id, string functionName, string? detail = null)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
            FunctionStarted?.Invoke(this, new AiFunctionStartedEvent
            {
                Id = id,
                FunctionName = functionName,
                Detail = detail
            }));
    }

    private async Task NotifyFunctionCompletedAsync(string id, Exception? exception = null)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
            FunctionCompleted?.Invoke(this, new AiFunctionCompletedEvent
            {
                Id = id,
                Result = exception == null,
                ToolOutput = exception is OperationCanceledException ? "Cancelled." : exception?.ToString()
            }));
    }

    private void RaiseFunctionProgress(string id, string output)
    {
        Dispatcher.UIThread.Post(() =>
            FunctionProgress?.Invoke(this, new AiFunctionProgressEvent
            {
                Id = id,
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
            provider._activeFunctions[id] = functionCancellationSource;

            var context = new AiFunctionInvocationContext(id,
                output => provider.RaiseFunctionProgress(id, output));
            Exception? exception = null;
            try
            {
                await provider.NotifyFunctionStartedAsync(id, friendlyName!, detail);

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
                await provider.NotifyFunctionCompletedAsync(id, exception);
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
