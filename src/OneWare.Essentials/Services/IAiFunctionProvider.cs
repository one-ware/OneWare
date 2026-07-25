using Microsoft.Extensions.AI;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IAiFunctionProvider
{
    /// <summary>Fired when an AI function starts.</summary>
    event EventHandler<AiFunctionStartedEvent>? FunctionStarted;
    /// <summary>Fired when an AI function completes.</summary>
    event EventHandler<AiFunctionCompletedEvent>? FunctionCompleted;
    /// <summary>Fired when a running AI function reports incremental output.</summary>
    event EventHandler<AiFunctionProgressEvent>? FunctionProgress
    {
        add { }
        remove { }
    }

    /// <summary>Returns available AI tools for this provider.</summary>
    ICollection<AIFunction> GetTools();

    /// <summary>Cancels all AI functions that are currently running.</summary>
    void CancelActiveFunctions()
    {
    }

    /// <summary>Cancels the single running AI function with the given invocation id.</summary>
    void CancelFunction(string id)
    {
    }

    /// <summary>Registers an additional AI function (e.g. from plugins).</summary>
    void RegisterFunction(IOneWareAiFunction function);

    /// <summary>Registers an additional system prompt segment.</summary>
    void RegisterPromptAddition(string promptAddition);

    /// <summary>Returns all registered prompt additions.</summary>
    IReadOnlyCollection<string> GetPromptAdditions();

    /// <summary>
    /// Returns the <see cref="IOneWareAiFunction.ConfirmationCheck"/> delegate for the named function,
    /// or <see langword="null"/> if the function has no check or is not registered.
    /// </summary>
    Func<AIFunctionArguments, string?>? GetConfirmationCheck(string functionName);
}
