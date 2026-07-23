using System.Collections.Concurrent;
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
    private readonly Lock _registrationLock = new();
    private readonly List<IOneWareAiFunction> _registeredFunctions = [];
    private readonly List<string> _promptAdditions = [];
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
        foreach (var cancellationSource in _activeFunctions.Values)
        {
            try
            {
                cancellationSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The function completed while cancellation was being requested.
            }
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
