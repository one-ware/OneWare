using Microsoft.Extensions.AI;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IAiFunctionProvider
{
    /// <summary>
    /// Fired when an AI function starts.
    /// </summary>
    event EventHandler<AiFunctionStartedEvent>? FunctionStarted;
    /// <summary>
    /// Fired when an AI function requires user approval before execution.
    /// </summary>
    event EventHandler<AiFunctionPermissionRequestEvent>? FunctionPermissionRequested;
    /// <summary>
    /// Fired when an AI function completes.
    /// </summary>
    event EventHandler<AiFunctionCompletedEvent>? FunctionCompleted;
    /// <summary>
    /// Returns available AI tools for this provider.
    /// </summary>
    ICollection<AIFunction> GetTools();

    /// <summary>
    /// Registers an additional AI function (e.g. from plugins).
    /// </summary>
    void RegisterFunction(IOneWareAiFunction function);

    /// <summary>
    /// Registers an additional system prompt segment.
    /// </summary>
    void RegisterPromptAddition(string promptAddition);

    /// <summary>
    /// Returns all registered prompt additions.
    /// </summary>
    IReadOnlyCollection<string> GetPromptAdditions();
}
