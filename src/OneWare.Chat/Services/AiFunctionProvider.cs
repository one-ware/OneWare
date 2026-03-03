using Avalonia.Threading;
using Microsoft.Extensions.AI;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Chat.Services;

public class AiFunctionProvider(
    IProjectExplorerService projectExplorerService,
    IMainDockService dockService,
    IErrorService errorService,
    ITerminalManagerService terminalManagerService,
    IWindowService windowService,
    AiFileEditService aiFileEditService) : IAiFunctionProvider
{
    private readonly HashSet<string> _allowedForSession = new(StringComparer.Ordinal);
    private readonly Lock _registrationLock = new();
    private readonly List<IOneWareAiFunction> _registeredFunctions = [];
    private readonly List<string> _promptAdditions = [];
    private bool _builtInsRegistered;

    public event EventHandler<AiFunctionStartedEvent>? FunctionStarted;
    public event EventHandler<AiFunctionPermissionRequestEvent>? FunctionPermissionRequested;
    public event EventHandler<AiFunctionCompletedEvent>? FunctionCompleted;

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

    private async Task<AiFunctionPermissionDecision> RequestPermissionAsync(
        string functionName,
        string question,
        string? detail)
    {
        var requestId = Guid.NewGuid().ToString();
        var decisionSource =
            new TaskCompletionSource<AiFunctionPermissionDecision>(TaskCreationOptions.RunContinuationsAsynchronously);

        await Dispatcher.UIThread.InvokeAsync(() =>
            FunctionPermissionRequested?.Invoke(this, new AiFunctionPermissionRequestEvent
            {
                Id = requestId,
                FunctionName = functionName,
                Question = question,
                Detail = detail,
                DecisionSource = decisionSource
            }));

        return await decisionSource.Task.ConfigureAwait(false);
    }

    private async Task EnsurePermissionAsync(
        string friendlyName,
        bool requiresPermission,
        string? permissionScope,
        string? permissionQuestion,
        string? permissionDetail)
    {
        if (!requiresPermission) return;

        var scope = string.IsNullOrWhiteSpace(permissionScope) ? friendlyName : permissionScope;
        if (_allowedForSession.Contains(scope))
            return;

        var question = string.IsNullOrWhiteSpace(permissionQuestion)
            ? $"Allow {friendlyName}?"
            : permissionQuestion;
        var detail = string.IsNullOrWhiteSpace(permissionDetail) ? null : permissionDetail;
        var decision = await RequestPermissionAsync(friendlyName, question, detail);

        switch (decision)
        {
            case AiFunctionPermissionDecision.AllowForSession:
                _allowedForSession.Add(scope);
                return;
            case AiFunctionPermissionDecision.AllowOnce:
                return;
            default:
                throw new InvalidOperationException($"{friendlyName} was denied by user.");
        }
    }

    private async Task<string> NotifyFunctionStartedAsync(string functionName, string? detail = null)
    {
        var id = Guid.NewGuid().ToString();

        await Dispatcher.UIThread.InvokeAsync(() =>
            FunctionStarted?.Invoke(this, new AiFunctionStartedEvent
            {
                Id = id,
                FunctionName = functionName,
                Detail = detail
            }));

        return id;
    }

    private async Task NotifyFunctionCompletedAsync(string id, Exception? exception = null)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
            FunctionCompleted?.Invoke(this, new AiFunctionCompletedEvent
            {
                Id = id,
                Result = exception == null,
                ToolOutput = exception?.ToString() ?? $"Tool {id} succeeded."
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
            var permissionDetail = definition.PermissionDetailFactory?.Invoke(arguments) ?? definition.PermissionDetail;

            await provider.EnsurePermissionAsync(
                friendlyName!,
                definition.RequirePermission,
                definition.PermissionScope,
                definition.PermissionQuestion,
                permissionDetail);

            var id = await provider.NotifyFunctionStartedAsync(friendlyName!);
            Exception? exception = null;
            try
            {
                if (definition.RunOnUiThread)
                {
                    return await Dispatcher.UIThread.InvokeAsync(async () =>
                        await base.InvokeCoreAsync(arguments, cancellationToken));
                }

                return await base.InvokeCoreAsync(arguments, cancellationToken);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                await provider.NotifyFunctionCompletedAsync(id, exception);
            }
        }
    }
}
